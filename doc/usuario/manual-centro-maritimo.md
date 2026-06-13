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
