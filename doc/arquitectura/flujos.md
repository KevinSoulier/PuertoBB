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
  3. Confirma via IDialogService.ConfirmarAsync("¿Generar recibos para N agencias?")
  4. Llama ICentroMaritimoReciboService.CerrarPeriodoAsync(anio, mes, ct)
  5. Muestra resultado (éxitos / errores por agencia)

Service (ICentroMaritimoReciboService.CerrarPeriodoAsync):
  Para cada Agencia con vouchers pendientes en (anio, mes):
    a. Verificar que no exista ya un Recibo consolidado para (AgenciaId, anio, mes)
       → si existe: skip con warning "ya cerrado"
    b. Calcular:
         importe = vouchers.Sum(v => v.Importe)
         detalle = "Vouchers Nros: " + string.Join(", ", vouchers.OrderBy(v => v.Numero).Select(v => v.Numero))
    c. Construir Recibo (EsConsolidadoVouchers=true, Importe=importe, Detalle=detalle)
       Copiar datos de apoderado desde Configuracion si UsarApoderado=true
    d. IAfipService.ObtenerCAEAsync(recibo) → recibo.CAE, recibo.FechaVencimientoCAE
    e. ICentroMaritimoPdfService.GenerarPdfConsolidadoAsync(recibo, vouchers)
       → PDF = [página recibo AFIP] + [página(s) por cada voucher]
    f. IMailService.EnviarReciboAsync(agencia.Email, adjunto=pdfConsolidado, ct)
    g. recibo.Estado = ReciboEstado.Enviado  (porque el mail se envió)
    h. Asignar vouchers.ReciboId = recibo.Id
    i. Persistir: IReciboRepository.AddAsync(recibo) + IVoucherRepository.MarcarConsolidadosAsync(...)

  Return ServiceResult<List<ResultadoCierrePorAgencia>>
  (ResultadoCierrePorAgencia = { Agencia, Exito, ErrorMessage? })

Infrastructure:
  IVoucherRepository.GetPendientesByPeriodoAsync(anio, mes)
    → vouchers WHERE ReciboId IS NULL AND PeriodoAnio=anio AND PeriodoMes=mes
  IReciboRepository.ExisteConsolidadoAsync(agenciaId, anio, mes) → bool
  IReciboRepository.AddAsync(recibo)
  IVoucherRepository.MarcarConsolidadosAsync(IEnumerable<int> voucherIds, int reciboId)

Core (estado final):
  - Recibo creado: Estado=Enviado, EsConsolidadoVouchers=true, CAE asignado
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
  1. Llama IReciboService.GetDuplicadosAsync(grupoId, anio, mes)
     → si ya existen: advertir "N empresas ya tienen recibo en este período"
  2. Confirma via IDialogService
  3. Llama IReciboService.EmitirMasivoAsync(grupoId, anio, mes, ct)
  4. Muestra resultado por empresa

Service (IReciboService.EmitirMasivoAsync):
  Obtener grupo con empresas/agencias
  Para cada Empresa/Agencia del grupo:
    a. Verificar no duplicado: (EntidadId, GrupoId, anio, mes)
    b. Construir Recibo (Importe=grupo.Importe, Detalle=grupo.Nombre, EsConsolidadoVouchers=false)
    c. IAfipService.ObtenerCAEAsync(recibo) → recibo.Estado = Emitido
    d. IReciboRepository.AddAsync(recibo)
    e. IPdfService.GenerarPdfReciboAsync(recibo) → pdf (regenerado a demanda, no se persiste)
    f. IMailService.EnviarReciboAsync(entidad.Emails, pdf, ct)
       → éxito:  recibo.Estado = Enviado
       → fallo:  recibo.Estado queda Emitido; se registra en ResultadoEmisionPorEntidad.ErrorMail
    g. IReciboRepository.UpdateAsync(recibo)

  Return ServiceResult<List<ResultadoEmisionPorEntidad>>

Infrastructure:
  IGrupoFacturacionRepository.GetConMiembrosAsync(grupoId)
  IReciboRepository.ExisteAsync(entidadId, grupoId, anio, mes) → bool
  IReciboRepository.AddAsync(recibo)
```

---

## 3. Emisión individual — fuera del ciclo masivo

El destinatario siempre debe existir en el sistema (Empresa en CP, Agencia en CM).
Cubre tanto cobros puntuales como cobros extraordinarios de una sola entidad.

```
Trigger: Laura selecciona empresa/agencia, completa importe y detalle, pulsa "Emitir"

ViewModel → IReciboService.EmitirIndividualAsync(entidadId, importe, detalle, anio, mes, ct)
  a. Construir Recibo (GrupoFacturacionId=null, Detalle=ingresado por Laura)
  b. IAfipService.ObtenerCAEAsync(recibo)        → recibo.Estado = Emitido
  c. IReciboRepository.AddAsync(recibo)
  d. IPdfService.GenerarPdfReciboAsync(recibo)   → pdf (regenerado a demanda, no se persiste)
  e. IMailService.EnviarReciboAsync(emails, pdf, ct)
     → éxito:  recibo.Estado = Enviado
     → fallo:  recibo.Estado queda Emitido; se muestra advertencia; Laura puede reenviar desde dashboard
  f. IReciboRepository.UpdateAsync(recibo)
```

---

## 4. Nota de crédito

```
Trigger: Laura selecciona un recibo emitido y pulsa "Anular"

ViewModel → dialog con:
  - Confirmación: "¿Anular recibo Nro X de [Empresa] período MM/YYYY?"
  - Checkbox: "Enviar notificación por mail a [email(s)]" (default: true)

ViewModel → IReciboService.AnularReciboAsync(reciboId, enviarMail, ct)
  a. Construir NotaDeCredito referenciando el Recibo original
  b. IAfipService.ObtenerCAEAsync(notaDeCredito)
  c. IPdfService.GenerarPdfNotaDeCreditoAsync(notaDeCredito) → pdf
  d. Si enviarMail=true:
       IMailService.EnviarReciboAsync(emails, pdf, ct)
  e. recibo.Estado = ReciboEstado.Anulado
  f. IReciboRepository.UpdateAsync(recibo)
  g. INotaDeCreditoRepository.AddAsync(notaDeCredito)
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
  a. IPdfService.GenerarPdfReciboAsync(recibo)  → pdf (regenerado a demanda)
  b. IMailService.EnviarReciboAsync(emails, pdf, ct)
     → éxito: recibo.Estado = Enviado; IReciboRepository.UpdateAsync(recibo)
     → fallo: mostrar error; estado no cambia
```

---

## 7. Marcar recibo como pagado

```
Trigger: Laura selecciona un recibo en el dashboard y pulsa "Marcar como pagado"

ViewModel → IReciboService.MarcarPagadoAsync(reciboId, ct)
  a. recibo.Estado    = ReciboEstado.Pagado
  b. recibo.FechaPago = DateTime.Today
  c. IReciboRepository.UpdateAsync(recibo)
```

---

## 8. Dashboard de pendientes / control de pagos

```
Trigger: Laura abre la sección "Pendientes"

ViewModel → IReciboService.GetPendientesAsync(filtros, ct)

Filtros disponibles:
  - Por período (PeriodoAnio + PeriodoMes)
  - Por grupo/generación (GrupoFacturacionId)
  - Por estado persistido (Emitido / Enviado)
  - Por empresa/agencia

Datos mostrados por fila:
  Empresa/Agencia | Período | Importe | Estado | FechaEmision | FechaVencimientoPago | Días de atraso

"Días de atraso" = max(0, (DateTime.Today - FechaVencimientoPago).Days)
  → calculado en ViewModel al cargar, no persistido en DB
  → fila se destaca en rojo si FechaVencimientoPago < DateTime.Today && Estado != Pagado/Anulado

Nota: "Vencido" no es un estado persistido — es un estado visual derivado de FechaVencimientoPago.
El enum ReciboEstado solo tiene: Emitido, Enviado, Pagado, Anulado.
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
}

interface ICentroMaritimoPdfService
{
    Task<byte[]> GenerarPdfVoucherAsync(Voucher voucher, CancellationToken ct = default);
    Task<byte[]> GenerarPdfConsolidadoAsync(CentroMaritimo.Recibo recibo, IEnumerable<Voucher> vouchers, CancellationToken ct = default);
    Task<byte[]> GenerarPdfNotaDeCreditoAsync(CentroMaritimo.NotaDeCredito nc, CancellationToken ct = default);
}

// Mail — compartido (solo varía el contenido del adjunto)
interface IMailService
{
    Task<ServiceResult<bool>> EnviarReciboAsync(string destinatario, byte[] pdfAdjunto, string asunto, CancellationToken ct = default);
}

// AFIP — compartido
interface IAfipService
{
    Task<ServiceResult<CaeResult>> ObtenerCAEAsync(ComprobanteAfipRequest request, CancellationToken ct = default);
}
```

> **Decisión de diseño — IPdfService dividido:**
> La interfaz de PDF se divide en dos (`ICamaraPortuariaPdfService` / `ICentroMaritimoPdfService`) porque los documentos son estructuralmente distintos: Centro Marítimo necesita `GenerarPdfConsolidadoAsync` que no existe en Cámara Portuaria. Mantenerlos separados evita métodos vacíos/no implementados y deja cada contrato expresivo.
