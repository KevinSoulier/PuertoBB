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
período expandibles. El flag de estado por agencia se deriva de los vouchers libres y de
los `Recibo` consolidados de la agencia (puede haber más de uno: original + complementarios):

| Flag UI | Condición |
|---|---|
| **Pendiente** | Hay vouchers libres por consolidar (1ª emisión o complementario), o un consolidado sin CAE |
| **Emitido** | No quedan vouchers libres; algún consolidado con CAE aún sin enviar por mail |
| **Completo** | No quedan vouchers libres y todos los consolidados están enviados (o cobrados) |

Al **anular** un consolidado se emite la nota de crédito y sus vouchers quedan
**desvinculados** (vuelven a estar libres): la agencia aparece de nuevo como **Pendiente**
y el período se puede reemitir (el anulado y su NC quedan en el historial de Recibos).

El mapeo por recibo está en `VoucherService.MapEstado`; la agregación por agencia y la lista
de consolidados se arman en `VoucherService.GetCierrePeriodoAsync`, que devuelve
`AgenciaCierrePeriodoVm` con los vouchers (cada uno con su comprobante o "Libre"), el total,
el estado calculado y la lista `Consolidados`.

---

## Recibo consolidado complementario (voucher olvidado tras emitir)

No existe un "período cerrado" que bloquee la carga de vouchers: cerrar es consolidar +
CAE + PDF + mail, no un candado. Si después de emitir aparece un **voucher olvidado**, se
carga normalmente y queda **libre** (`ReciboId IS NULL`) en su período.

- **Si el consolidado todavía está Pendiente** (sin CAE, p. ej. AFIP falló): volver a emitir
  lo **incorpora al mismo recibo** (reintento idempotente, Pendiente-first).
- **Si el consolidado ya tiene CAE** (Emitido/Completo): no se puede tocar el comprobante
  autorizado. Volver a **Emitir/Cerrar** genera un **recibo consolidado complementario** —
  un comprobante **adicional** con su propio número y CAE, solo por los vouchers libres— y
  **deja intacto el original** (sin Nota de Crédito). El recibo anterior y el complementario
  conviven en el período.

**Invariante:** se admite **un solo consolidado Pendiente (sin CAE) por `(Agencia, Período)`**
—evita dos emisiones en curso a la vez—, pero **varios consolidados con CAE** (original +
complementarios). Lo garantiza el índice único parcial de `Recibo`
(`EsConsolidadoVouchers=1 AND EstadoFiscal='Pendiente'`).

> La alternativa más pesada de **anular + reemitir todo** sigue disponible (emite NC, libera
> todos los vouchers y reemite el período): conviene cuando hay que corregir/eliminar vouchers
> ya consolidados, no solo agregar uno olvidado.

---

## Acciones disponibles

Por **agencia** (fila), sobre los **vouchers libres** (lo que se va a emitir):

| Acción | Aplica cuando | Resultado |
|---|---|---|
| **Previsualizar / Descargar PDF** (libres) | Hay vouchers libres | Concatenación de los PDFs de los vouchers libres del período. |
| **Emitir recibo** | Pendiente | Fases 1→2 (consolidar + CAE) de los vouchers libres; genera un **complementario** si la agencia ya tiene un consolidado. |

Por **recibo consolidado** (en el detalle expandible, una fila por consolidado — original o complementario):

| Acción | Resultado |
|---|---|
| **Previsualizar / Descargar PDF** | Recibo (con CAE+QR) + sus vouchers concatenados. |
| **Enviar mail** | PDF único del recibo a los emails activos de la agencia. |

Por **voucher** (en el detalle expandible, una fila por voucher — para agencias que facturan
por voucher individual, ver la sección siguiente):

| Acción | Aplica cuando | Resultado |
|---|---|---|
| **Ver PDF** | Siempre | El recibo (con CAE+QR) si el voucher ya está emitido; el PDF del voucher si está libre. |
| **Emitir** | Voucher libre (o una emisión individual previa que quedó Pendiente) | Genera el recibo AFIP de ese voucher solo, sin enviar mail. |
| **Emitir y Enviar** | Voucher libre o ya emitido individualmente | Si no está emitido, emite y envía; si ya está emitido, **solo reenvía** el mail. |

Globales (toolbar):

| Acción | Resultado |
|---|---|
| **Emitir recibos** | Fases 1→2 para todas las agencias pendientes, sin mail. |
| **Enviar mails** | Fases 3→4 de todos los consolidados del período (incluye complementarios). |
| **Cerrar período** | Fases 1→4 completas para todas las agencias pendientes. |

En la página de **Vouchers** además se puede:

- **Previsualizar** un voucher individual: genera el PDF y lo abre con el visor por
  defecto del SO (temporal).
- **Descargar** un voucher individual: guarda el PDF en disco.

Ambas operaciones usan `CentroMaritimoPdfService.GenerarPdfVoucherAsync`.

---

## Facturación individual por voucher

Algunas agencias piden que **cada voucher se facture por separado** (un recibo por voucher),
en lugar del consolidado. Esto se hace desde la misma ventana de **Cierre de período**, con los
botones **Ver PDF / Emitir / Emitir y Enviar** de cada fila de voucher (no requiere configuración
por agencia: el operador decide voucher por voucher).

- Un recibo individual se modela como una **`Consolidacion` de un solo voucher** con el flag
  `Individual = true`. Reutiliza todo el flujo del consolidado (CAE idempotente, recuperación
  anti-duplicado, PDF recibo+voucher, mail, anulación con NC y bloqueo de edición).
- A diferencia del consolidado, **pueden coexistir varios recibos individuales Pendientes** por
  `(Agencia, Período)`: el índice único parcial de `Consolidacion`
  (`Pendiente = 1 AND Individual = 0`) y las consultas de reintento del consolidado
  (`GetConsolidacionPendienteAsync`, `GetClientesConConsolidacionPendienteAsync`) **excluyen** los
  individuales, así que el cierre masivo nunca los trata como work-in-progress.
- El método de negocio es `CentroMaritimoReciboService.EmitirVoucherAsync(voucherId, enviarMail)`.

**Convivencia con el cierre masivo:** "Emitir recibos" y "Cerrar período" siguen **consolidando
los vouchers que queden libres**. Para una agencia que factura por voucher, emitir cada voucher
individualmente **antes** de correr el masivo (una vez emitido, el voucher deja de estar libre y el
masivo no lo toca).

**Corregir un voucher ya emitido:** anular su recibo desde la página de **Recibos**
(`Anular` / `Anular y enviar`): se emite la nota de crédito y el voucher queda **liberado**
(`ConsolidacionId = null`) y vuelve a ser editable. Luego se corrige en **Vouchers** y se vuelve a
emitir con los botones del cierre.

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
4. **Iteración 4 (✅ hecha):** facturación **individual por voucher** (botones Ver PDF / Emitir /
   Emitir y Enviar por fila de voucher; `Consolidacion.Individual`; `EmitirVoucherAsync`).
