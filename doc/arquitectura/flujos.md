# Flujos de negocio → técnicos

---

## 1. Cierre de período — Centro Marítimo

Genera un recibo por cada agencia que tenga vouchers pendientes en el período, y un PDF consolidado (recibo AFIP + vouchers).

```
Trigger: Laura selecciona mes/año y pulsa "Cerrar período"

UI (CentroMaritimo.UI):
  CierrePeriodoView → CierrePeriodoCommand
  Muestra ProgressBar (IsBusy = true)

ViewModel:
  CierrePeriodoViewModel.EjecutarCierreAsync(anio, mes, ct)
  1. Llama IVoucherService.GetAgenciasConVouchersPendientesAsync(anio, mes)
  2. Muestra resumen al usuario (N agencias, M vouchers)
  3. Confirma via IDialogService.ConfirmarAsync
  4. Llama ICentroMaritimoReciboService.CerrarPeriodoAsync(anio, mes, ct)
  5. Muestra resultado (éxitos / errores por agencia)

Service (ICentroMaritimoReciboService.CerrarPeriodoAsync):
  Guard: si config.PuntoDeVentaActivo es null → error "Configure un punto de venta activo"
  Para cada Agencia con vouchers pendientes en (anio, mes):
    Flujo Pendiente-first (idempotente):
    a. GetConsolidadoAsync(agenciaId, anio, mes):
       → si existe y EsCompleto → skip ("Ya existe recibo consolidado")
       → si existe Pendiente → re-sincronizar (vincular nuevos vouchers, recalcular importe/líneas)
         → goto ProcesarReciboAsync(existente, ...)
       → si null → construir Recibo con Estado=Pendiente, EsConsolidadoVouchers=true
         → poblar Lineas: una por voucher ("Voucher {n} — {barco} — {fecha}")
         → copiar snapshot Receptor* desde agencia
         → AddConVouchersAsync(recibo, voucherIds) [atómico: recibo + FK de vouchers]
    b. ProcesarReciboAsync(recibo, agencia, config, enviarMail, ct):
       → si string.IsNullOrEmpty(recibo.CAE): IAfipService.ObtenerCAEAsync → recibo.CAE, FechaVencimientoCAE
       → generar PDF consolidado (recibo + vouchers)
       → si enviarMail: IMailService.EnviarReciboAsync → recibo.FechaEnvioMail (envío "Enviado" derivado)
       → IReciboRepository.UpdateAsync(recibo)

  Return ServiceResult<List<ResultadoCierrePorAgencia>>

Infrastructure:
  IReciboRepository.GetConsolidadoAsync(agenciaId, anio, mes) — excluye Anulados
  IReciboRepository.AddConVouchersAsync(recibo, voucherIds)   — un único SaveChanges atómico
  IReciboRepository.ExisteConsolidadoAsync                    — excluye Anulados (para index check)

Core (estado final):
  - Recibo: Estado=Enviado, EsConsolidadoVouchers=true, CAE asignado, Lineas pobladas
  - Vouchers: ReciboId apuntando al recibo
```

---

## 2. Emisión masiva de cuota social — Cámara Portuaria y Centro Marítimo

Genera un recibo por cada Empresa/Agencia del grupo en el período.

```
Trigger: Laura selecciona grupo, mes/año y pulsa "Emitir"

UI:
  EmisionMasivaView → EmitirCommand

ViewModel:
  EmisionMasivaViewModel.EmitirAsync(grupoId, anio, mes, ct)
  1. Muestra estado por entidad (ya emitido / pendiente)
  2. Confirma via IDialogService
  3. Llama IReciboService.EmitirMasivoAsync(grupoId, anio, mes, ct)
  4. Muestra resultado por empresa/agencia

Service (IReciboService.EmitirMasivoAsync):
  Guard: si config.PuntoDeVentaActivo es null → error
  Obtener grupo con empresas/agencias y sus Lineas
  Para cada Empresa/Agencia del grupo:
    EmitirOResumirAsync(entidadId, grupo, anio, mes, config, enviarMail, ct):
      a. Anti-duplicado por EmisionGrupo (índice único GrupoId+EntidadId+PeriodoAnio+PeriodoMes)
         → si ya existe completo: skip
         → si existe Pendiente: retomar (re-sync + obtener CAE)
      b. Construir Recibo con Estado=Pendiente
         → poblar Lineas desde grupo.Lineas
         → copiar snapshot Receptor* (desde empresa/agencia: Nombre, RazonSocial, Cuit, ...)
      c. Crear EmisionGrupo + AddAsync(recibo) — persiste antes de pedir CAE
      d. IAfipService.ObtenerCAEAsync → recibo.EstadoFiscal = Emitido
         → si falla: recibo.UltimoErrorCae = mensaje; queda Pendiente (reintentable)
      e. IPdfService.GenerarPdfReciboAsync(recibo) → pdf (regenerado a demanda, no se persiste)
      f. IMailService.EnviarReciboAsync(entidad.Emails, pdf, ct)
         → éxito:  recibo.FechaEnvioMail = DateTime.Now (estado "Enviado" derivado)
         → fallo:  recibo.UltimoErrorMail = mensaje; EstadoFiscal queda Emitido
      g. UpdateAsync(recibo)

  Return ServiceResult<List<ResultadoEmisionPorEntidad>>

Infrastructure:
  IGrupoFacturacionRepository.GetConMiembrosAsync(grupoId)
  IReciboRepository.GetPorClaveAsync(entidadId, grupoId, anio, mes)
    → retorna recibo Pendiente si existe (permite reintento); null si no hay ninguno Pendiente
  IReciboRepository.AddAsync(recibo)   (implica también AddEmisionGrupoAsync)
```

---

## 3. Emisión individual — fuera del ciclo masivo

El destinatario siempre debe existir en el sistema (Empresa en CP, Agencia en CM).
Se permiten N recibos individuales por (entidad, período) — no hay restricción de uno por período.

```
Trigger: Laura selecciona empresa/agencia, completa importe y líneas, pulsa "Emitir"

ViewModel → IReciboService.EmitirOResumirAsync(entidadId, grupoId=null, ...)
  a. GetPorClaveAsync(entidadId, grupoId=null, anio, mes)
     → si existe Pendiente individual (sin EmisionGrupo, sin EsConsolidadoVouchers): retomar
     → si no: construir nuevo Recibo con EstadoFiscal=Pendiente, EmisionGrupo=null
  b. Poblar Lineas (concepto/importe ingresados por Laura)
  c. Copiar snapshot Receptor* desde la entidad
  d. AddAsync(recibo) → persistir Pendiente
  e. IAfipService.ObtenerCAEAsync → recibo.EstadoFiscal = Emitido
  f. IPdfService.GenerarPdfReciboAsync(recibo)
  g. IMailService.EnviarReciboAsync(emails, pdf, ct)
     → éxito:  recibo.FechaEnvioMail = DateTime.Now (estado "Enviado" derivado)
     → fallo:  recibo.EstadoFiscal queda Emitido; mostrar advertencia
  h. UpdateAsync(recibo)
```

---

## 4. Nota de crédito

```
Trigger: Laura selecciona un recibo emitido y pulsa "Anular"

ViewModel → dialog con:
  - Confirmación: "¿Anular recibo Nro X de [Empresa/Agencia] período MM/YYYY?"
  - Checkbox: "Enviar notificación por mail a [email(s)]" (default: true)

ViewModel → IReciboService.AnularReciboAsync(reciboId, enviarMail, ct)
  Guard: si config.PuntoDeVentaActivo es null → error
  a. Construir NotaDeCredito referenciando el Recibo original
     → CodigoAfip = CatalogoComprobantesAfip.NcPara(recibo.CodigoAfip)
  b. IAfipService.ObtenerCAEAsync(notaDeCredito)
  c. IPdfService.GenerarPdfNotaDeCreditoAsync(notaDeCredito) → pdf
     → el PDF incluye sección "Comprobante asociado: Recibo C XXXX-XXXXXXXX"
  d. Si enviarMail=true:
       IMailService.EnviarReciboAsync(emails, pdf, ct)
  e. IReciboRepository.AnularConNotaAsync(recibo, nota, ct) [atómico: estado+NC en un SaveChanges]
     → CM además desvincula vouchers del consolidado (v.ReciboId = null) para permitir reemisión
```

---

## 5. Alta de voucher — Centro Marítimo

```
Trigger: Laura carga un nuevo voucher para una agencia

ViewModel → IVoucherService.CrearVoucherAsync(agenciaId, barcoId, fecha, importe, ct)
  a. IContadorVoucherService.ObtenerSiguienteNumeroAsync() → numero (incrementa ContadorVoucher.UltimoNumero)
  b. Derivar PeriodoAnio y PeriodoMes de fecha
  c. Construir Voucher
  d. IVoucherRepository.AddAsync(voucher)
  Return ServiceResult<Voucher> con el número asignado
```

---

## 6. Reenviar mail de un recibo

Para recibos en estado Emitido (mail falló o Laura quiere reenviar).

```
Trigger: Laura selecciona recibo con Estado=Emitido y pulsa "Reenviar mail"

ViewModel → IReciboService.ReenviarMailAsync(reciboId, ct)
  Guard: si recibo.CAE vacío → error "El recibo no tiene CAE: emítalo antes de reenviar"
  a. IPdfService.GenerarPdfReciboAsync(recibo)  → pdf (regenerado a demanda)
  b. IMailService.EnviarReciboAsync(emails, pdf, ct)
     → éxito: recibo.FechaEnvioMail = DateTime.Now; UpdateAsync (estado "Enviado" derivado)
     → fallo: mostrar error; estado no cambia
```

---

## 7. Marcar recibo como pagado

```
Trigger: Laura selecciona un recibo en el dashboard y pulsa "Marcar como pagado"

ViewModel → IReciboService.MarcarPagadoAsync(reciboId, ct)
  a. recibo.FechaPago = DateTime.Today   (el cobro "Pagado" se deriva de esta fecha)
  b. IReciboRepository.UpdateAsync(recibo)
```

---

## 8. Dashboard de pendientes / control de pagos

```
Trigger: Laura abre la sección "Pendientes"

ViewModel → IReciboService.GetPendientesAsync(filtros, ct)

Filtros disponibles:
  - Por período (PeriodoAnio + PeriodoMes)
  - Solo vencidos (FechaVencimientoPago < hoy && Estado != Pagado/Anulado)
  - Por empresa/agencia

Datos mostrados por fila:
  Empresa/Agencia | Período | Importe | Estado | FechaEmision | FechaVencimientoPago | Días de atraso

"Días de atraso" = max(0, (DateTime.Today - FechaVencimientoPago).Days)
  → calculado en ViewModel al cargar, no persistido en DB
  → fila destacada en naranja (#FFF3E0) si vencida, en verde suave si pagada

Estados del recibo (persistidos):
  Emitido → Enviado → Pagado (flujo normal)
             ↓
           Anulado (vía NC)
  Pendiente → Emitido (reintento exitoso de CAE)
"Vencido" NO es un estado: se calcula en presentación a partir de FechaVencimientoPago.
```

---

## Interfaces de servicio implicadas

```csharp
// Core/Interfaces/Services/

// PDF — separado por app porque los documentos son distintos
interface ICamaraPortuariaPdfService
{
    Task<byte[]> GenerarPdfReciboAsync(CamaraPortuaria.Recibo recibo, CancellationToken ct = default);
    Task<byte[]> GenerarPdfNotaDeCreditoAsync(CamaraPortuaria.NotaDeCredito nc, CancellationToken ct = default);
    Task<byte[]> GenerarPdfDescargaAsync(CamaraPortuaria.Recibo recibo, CancellationToken ct = default); // preview/consolidado
}

interface ICentroMaritimoPdfService
{
    Task<byte[]> GenerarPdfReciboAsync(CentroMaritimo.Recibo recibo, CancellationToken ct = default);
    Task<byte[]> GenerarPdfVoucherAsync(Voucher voucher, CancellationToken ct = default);
    Task<byte[]> GenerarPdfConsolidadoAsync(CentroMaritimo.Recibo recibo, CancellationToken ct = default);
    Task<byte[]> GenerarPdfNotaDeCreditoAsync(CentroMaritimo.NotaDeCredito nc, CancellationToken ct = default);
}

// Mail — compartido
interface IMailService
{
    Task<ServiceResult<bool>> EnviarReciboAsync(IEnumerable<string> destinatarios, byte[] pdfAdjunto, string asunto, CancellationToken ct = default);
}

// AFIP — compartido
interface IAfipService
{
    Task<ServiceResult<CaeResult>> ObtenerCAEAsync(ComprobanteAfipRequest request, CancellationToken ct = default);
    Task<ServiceResult<DiagnosticoAfip>> ProbarConexionAsync(int puntoVenta, int codigoComprobante, CancellationToken ct = default);
}
```

> **Decisión de diseño — IPdfService dividido:**
> La interfaz de PDF se divide en dos porque los documentos son estructuralmente distintos: Centro Marítimo necesita `GenerarPdfConsolidadoAsync` que no existe en Cámara Portuaria. Mantenerlos separados deja cada contrato expresivo y evita métodos vacíos.
