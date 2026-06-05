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
    c. IAfipService.ObtenerCAEAsync(recibo)
    d. IPdfService.GenerarPdfReciboAsync(recibo)
    e. IMailService.EnviarReciboAsync(entidad.Email, pdf, ct)
    f. recibo.Estado = ReciboEstado.Enviado
    g. IReciboRepository.AddAsync(recibo)

  Return ServiceResult<List<ResultadoEmisionPorEntidad>>

Infrastructure:
  IGrupoFacturacionRepository.GetConMiembrosAsync(grupoId)
  IReciboRepository.ExisteAsync(entidadId, grupoId, anio, mes) → bool
  IReciboRepository.AddAsync(recibo)
```

---

## 3. Emisión individual — fuera del ciclo masivo

```
Trigger: Laura selecciona empresa/agencia y pulsa "Emitir recibo individual"

ViewModel → IReciboService.EmitirIndividualAsync(entidadId, importe, detalle, anio, mes, ct)
  a. Construir Recibo (GrupoFacturacionId=null)
  b. IAfipService.ObtenerCAEAsync(recibo)
  c. IPdfService.GenerarPdfReciboAsync(recibo)
  d. IMailService.EnviarReciboAsync(...)
  e. IReciboRepository.AddAsync(recibo)
```

---

## 4. Nota de crédito

```
Trigger: Laura selecciona un recibo emitido y pulsa "Anular"

ViewModel → IReciboService.AnularReciboAsync(reciboId, ct)
  a. IDialogService.ConfirmarAsync("¿Anular recibo Nro X?")
  b. Construir NotaDeCredito referenciando el Recibo original
  c. IAfipService.ObtenerCAEAsync(notaDeCredito)
  d. IPdfService.GenerarPdfNotaDeCreditoAsync(notaDeCredito)
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

## 6. Marcar recibo como pagado

```
Trigger: Laura selecciona un recibo en el dashboard y pulsa "Marcar como pagado"

ViewModel → IReciboService.MarcarPagadoAsync(reciboId, ct)
  a. recibo.Estado    = ReciboEstado.Pagado
  b. recibo.FechaPago = DateTime.Today
  c. IReciboRepository.UpdateAsync(recibo)
```

---

## 7. Actualización automática de vencidos (al iniciar la app)

```
Trigger: App.xaml.cs → OnStartup o al abrir el dashboard

Service: IReciboService.ActualizarVencidosAsync(ct)
  Obtener recibos con Estado IN (Emitido, Enviado) AND FechaVencimientoPago < DateTime.Today
  Para cada uno:
    recibo.Estado = ReciboEstado.Vencido
    IReciboRepository.UpdateAsync(recibo)
```

---

## 8. Dashboard de pendientes / control de pagos

```
Trigger: Laura abre la sección "Pendientes"

ViewModel → IReciboService.GetPendientesAsync(filtros, ct)

Filtros disponibles:
  - Por período (PeriodoAnio + PeriodoMes)
  - Por grupo/generación (GrupoFacturacionId)
  - Por estado (Emitido / Enviado / Vencido)
  - Por empresa/agencia

Datos mostrados por fila:
  Empresa/Agencia | Período | Importe | Estado | FechaEmision | FechaVencimientoPago | Días de atraso

"Días de atraso" = (DateTime.Today - FechaVencimientoPago).Days — solo para Vencido
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
