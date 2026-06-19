# Cierre de Período — Centro Marítimo

Documento de negocio del proceso de cierre mensual de vouchers en Centro Marítimo.
Complementa `centro-maritimo.md` con el detalle operativo del flujo.

---

## El proceso completo (multi-fase)

El cierre de un período convierte los **vouchers pendientes** de cada agencia en un único
recibo consolidado, lo entrega a la agencia, y deja todo el ciclo trazado en el sistema.
Tiene cuatro fases secuenciales por agencia. Si una falla, la siguiente no se ejecuta y el
estado del recibo refleja en qué paso quedó.

### Fase 1 — Consolidar

- Se agrupan los vouchers con `ReciboId IS NULL` del período `(Anio, Mes)` por agencia.
- El total a facturar es la suma de los importes de esos vouchers.
- La leyenda del recibo lista los números de voucher incluidos.

### Fase 2 — Obtener CAE (AFIP/ARCA WSFE)

- Se construye un `ComprobanteAfipRequest` (tipo Recibo C, código 211 por defecto) con el
  total y el CUIT del receptor.
- Se llama a `IAfipService.ObtenerCAEAsync` (WSAA → WSFE) y se recibe:
  - **`NumeroComprobante`** (correlativo del punto de venta)
  - **`Cae`** (código de autorización)
  - **`FechaVencimientoCae`**
- Si AFIP rechaza el comprobante, el flujo se interrumpe y el recibo queda sin persistir.

> **Aclaración técnica importante.** AFIP/ARCA **no devuelve el PDF del recibo**. WSFE
> sólo devuelve CAE + número de comprobante + vencimiento del CAE. El PDF del comprobante
> siempre lo genera el emisor localmente, e incluye el CAE y el código QR obligatorio de
> AFIP. En PuertoBB esto lo hace `CentroMaritimoPdfService.GenerarPdfReciboAsync`.

### Fase 3 — Generar PDF único

Al cerrar un período, lo que la agencia recibe es **un único PDF** con:

1. El PDF del recibo consolidado (con CAE + QR), generado por
   `CentroMaritimoPdfService.GenerarPdfReciboAsync`.
2. Los PDFs individuales de cada voucher incluido, generados por
   `CentroMaritimoPdfService.GenerarPdfVoucherAsync`.

Esos PDFs se **concatenan** con `IPdfMerger.Merge` (implementación `PdfMerger` sobre
PdfSharp). El método público es `CentroMaritimoPdfService.GenerarPdfDescargaAsync`,
que admite recibo opcional:

- Con recibo: descarga "completa" (recibo + vouchers).
- Sin recibo: descarga "parcial" sólo de vouchers, útil como vista previa antes de cerrar.

### Fase 4 — Enviar por mail

- El PDF único se envía como adjunto a los `EmailAgencia.Email` activos de la agencia.
- Si el mail se envía OK, se registra `Recibo.FechaEnvioMail` (el envío "Enviado" es derivado). Si falla,
  el recibo queda `Emitido` con `UltimoErrorMail` y se puede reintentar desde la página de Recibos.

---

## Estados visibles en la UI

La página de **Cierre de período** muestra una fila por agencia con los vouchers del
período expandibles. El flag de estado por agencia se deriva del `Recibo` consolidado:

| Flag UI | Condición | Derivado del consolidado |
|---|---|---|
| **Pendiente** | No hay recibo consolidado para `(Agencia, Período)`, o está anulado | — / `EstadoFiscal=Anulado` |
| **Emitido** | Recibo persistido con CAE, mail aún no enviado | `EstadoFiscal=Emitido` y `FechaEnvioMail=null` |
| **Completo** | Recibo enviado por mail (o ya cobrado) | `FechaEnvioMail!=null` o `FechaPago!=null` |

Al **anular** un consolidado se emite la nota de crédito y sus vouchers quedan
**desvinculados** (vuelven a estar libres): la agencia aparece de nuevo como **Pendiente**
y el período se puede reemitir con un consolidado nuevo (el anulado y su NC quedan en el
historial de Recibos).

El mapeo está implementado en `VoucherService.MapEstado` y se sirve a través de
`VoucherService.GetCierrePeriodoAsync`, que devuelve `AgenciaCierrePeriodoVm` con los
vouchers, el total y el estado calculado.

---

## Acciones disponibles por agencia

| Acción | Estado en que aplica | Resultado |
|---|---|---|
| **Descargar PDF** (parcial) | Pendiente | Concatenación de los PDFs de vouchers del período. |
| **Descargar PDF** (completa) | Emitido / Completo | Recibo (con CAE+QR) + vouchers concatenados. |
| **Emitir recibos** | Pendiente | Fases 1→2 (consolidar + CAE) para todas las agencias, sin mail. |
| **Enviar mails** | Emitido | Fases 3→4 (PDF único + mail) de los ya emitidos. |
| **Cerrar período** (masivo) | — | Fases 1→4 completas para todas las agencias pendientes (también disponible por agencia). |

En la página de **Vouchers** además se puede:

- **Previsualizar** un voucher individual: genera el PDF y lo abre con el visor por
  defecto del SO (temporal).
- **Descargar** un voucher individual: guarda el PDF en disco.

Ambas operaciones usan `CentroMaritimoPdfService.GenerarPdfVoucherAsync`.

---

## Plan de iteraciones

1. **Iteración 1 (✅ hecha):** UI agrupada por agencia, flag de 3 estados, descarga de PDF
   adaptativo (parcial/completo), acciones de voucher individual (descargar / previsualizar),
   capa `IPdfMerger` + `GenerarPdfDescargaAsync`.
2. **Iteración 2 (✅ hecha):** emisión por fila y **Cerrar período** masivo operativos, con
   reintento idempotente (consolidado Pendiente-first; el reintento incorpora vouchers
   nuevos del período).
3. **Iteración 3 (✅ hecha):** envío automático del PDF único (recibo + vouchers
   concatenados con `GenerarPdfDescargaAsync`) en el cierre y en el reenvío.
