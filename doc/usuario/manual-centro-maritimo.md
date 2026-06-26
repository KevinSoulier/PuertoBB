# Manual operativo — Centro Marítimo

Guía del día a día de la aplicación **Centro Marítimo** (vouchers por barco, recibos
consolidados y cuotas a agencias marítimas). La configuración inicial y el paso a producción
están en [paso-a-produccion.md](paso-a-produccion.md). Los estados de recibo y el control de
pagos funcionan igual que en Cámara Portuaria
([manual-camara-portuaria.md](manual-camara-portuaria.md)); acá se detalla lo propio de CM.

---

## Las pantallas (menú lateral)

| Página | Para qué sirve |
|---|---|
| **Inicio** | Control de pagos (igual que CP). |
| **Vouchers** | ABM de vouchers: un voucher por barco que ingresó al puerto, numeración automática. |
| **Cierre de período** | Consolidar los vouchers del mes en un recibo por agencia, con CAE y mail. |
| **Recibos** | Todos los recibos (consolidados e individuales): reintentar, anular, reenviar, pagar, PDF. |
| **Emisión masiva** | Cuota social por grupo (igual que CP). |
| **Agencias / Barcos / Grupos** | ABMs. |
| **Configuración** | Emisor, AFIP, correo, vouchers (importe predeterminado y contador), tema, backup. |

## Vouchers

1. En **Vouchers**: elegir agencia, barco, fecha e importe (el importe se precarga con el
   valor predeterminado de Configuración). El **número** lo asigna el sistema en secuencia.
2. **Buscar** filtra por período. **Editar / Eliminar** solo funcionan sobre vouchers
   todavía no consolidados; los consolidados quedan visibles como referencia (su estado se
   ve en Cierre de Período). **Previsualizar / Descargar** generan el PDF del voucher.
3. Doble clic en una fila = editar.

## Cierre de período (la operación mensual central)

En **Cierre de período**, elegir mes/año y **Refrescar**: se ve una fila por agencia con sus
vouchers del período (expandibles), el total y el estado (**Pendiente / Emitido / Completo**).

Botones (de menor a mayor alcance):

- **Emitir recibos** — consolida y pide CAE para todas las agencias con vouchers libres,
  **sin** mandar mails (útil para revisar antes de enviar).
- **Enviar mails** — manda el PDF único a las agencias ya emitidas.
- **Cerrar período** — todo junto: consolidar + CAE + mail por agencia.
- Por fila: las mismas acciones para **una** agencia.

Qué hace el cierre por cada agencia:

1. Junta los vouchers **libres** (no consolidados) del período y arma **un recibo
   consolidado**: una línea por voucher, total = suma de los importes.
2. Pide el **CAE** a AFIP.
3. Arma el **PDF único**: el recibo (con CAE + QR) seguido del PDF de cada voucher.
4. Lo manda por mail a los emails activos de la agencia.

Si un paso falla, el siguiente no corre y el estado lo refleja: un fallo de AFIP deja el
consolidado **Pendiente** (reintentable; si entre medio cargaste vouchers nuevos del mismo
período, el reintento los incorpora al mismo recibo); un fallo de mail deja **Emitido**
(se reenvía después).

## Me olvidé de cargar un voucher y ya cerré el período

No pasa nada: no hace falta anular ni rehacer todo.

1. Cargá el voucher olvidado como siempre (en **Vouchers**); queda **Libre** en su período.
2. Andá a **Cierre de período**: la agencia vuelve a figurar **Pendiente** (porque tiene un
   voucher libre). En el detalle, ese voucher aparece como **Libre**.
3. Tocá **Emitir recibo** (o **Cerrar período**):
   - Si el recibo anterior **todavía no tenía CAE**, el voucher se suma a ese mismo recibo.
   - Si el recibo anterior **ya estaba emitido**, se genera un **recibo complementario**
     (un comprobante adicional, con su propio número) solo por el voucher olvidado. **El
     recibo anterior queda intacto** (no se emite Nota de Crédito). El sistema te avisa en
     el cartel de confirmación que será complementario.

En el detalle de la agencia vas a ver la lista de **Recibos emitidos** (el original y el
complementario), cada uno con sus botones de previsualizar, descargar y enviar por mail.

> Usá la anulación (abajo) solo si necesitás **corregir o eliminar** vouchers que ya estaban
> en el recibo, no para agregar uno nuevo.

## Facturar cada voucher por separado (recibo por voucher)

Si una agencia pide un recibo **por cada voucher** (en vez del consolidado), en **Cierre de
período**, dentro del detalle de la agencia, cada fila de voucher tiene tres botones:

- **Ver PDF** — el recibo (con CAE + QR) si ese voucher ya está emitido; el PDF del voucher si
  todavía está libre.
- **Emitir** — genera el recibo AFIP **de ese voucher solo**, sin mandar mail.
- **Emitir y Enviar** — si no está emitido, lo emite y lo manda; si **ya está emitido**, solo
  **reenvía** el mail.

No hay que configurar nada por agencia: vos decidís voucher por voucher. Cada recibo individual es
independiente y tiene su propio número de comprobante.

> **Ojo con los botones masivos.** "Emitir recibos" y "Cerrar período" **consolidan** los vouchers
> que sigan **libres**. Para una agencia que factura por voucher, emití cada voucher
> individualmente **antes** de usar los botones masivos (una vez emitido, el voucher deja de estar
> libre y el masivo ya no lo toca).

**¿Te equivocaste en un voucher ya emitido?** Anulá su recibo desde **Recibos**
(`Anular` / `Anular y enviar`): se emite la nota de crédito, el voucher se **libera** y vuelve a ser
editable. Corregilo en **Vouchers** y volvé a emitirlo con el botón **Emitir** del cierre.

## Anular un consolidado y reemitir el período

1. En **Recibos**, anular el consolidado → se emite la **nota de crédito** y los vouchers
   quedan **liberados** (vuelven a estar disponibles).
2. Corregir lo que haga falta (agregar/editar/eliminar vouchers del período).
3. Volver a **Cierre de período** y emitir de nuevo: se genera un consolidado nuevo con
   todos los vouchers del período. El recibo anulado y su NC quedan en el historial.

## Cobros extraordinarios

- **Individual**: en Recibos, "Nuevo recibo" — funciona igual que en CP y **no choca** con el
  consolidado del mes: una agencia puede tener su consolidado de vouchers Y recibos
  individuales en el mismo período.
- **Cuota por grupo**: en Emisión masiva, igual que CP (grupos de agencias con ítems).

## Reenvío del consolidado

"Reenviar por correo" sobre un consolidado vuelve a armar el **PDF único completo**
(recibo + todos sus vouchers) y lo manda de nuevo.

## Backup y problemas

Igual que CP: backup/restauración en Configuración; logs en
`%LocalAppData%\PuertoBB\CentroMaritimo\Logs\`; versión en el título de la ventana.
