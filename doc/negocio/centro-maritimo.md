# Centro Marítimo — Lógica de negocio

## Descripción

El Centro Marítimo emite recibos a **agencias** marítimas. Es administrado por Laura de forma unipersonal, desde la misma oficina que la Cámara Portuaria.

## Agencias

La unidad de negocio de Centro Marítimo es la **Agencia** marítima. Actualmente ~13 agencias asociadas.

## Grupos de facturación (cuota social)

Al igual que en Cámara Portuaria, las agencias se organizan en **Grupos de Facturación** para la emisión masiva de recibos de cuota social.

- Mismo importe para todas las agencias dentro de un grupo
- Grupos dinámicos: cantidad variable de agencias, modificables en cualquier momento

## Vouchers

- Cada voucher representa un barco que ingresó al puerto gestionado por esa agencia
- Pueden existir múltiples vouchers de la misma agencia en el mismo mes
- Los vouchers tienen numeración por **serie** con control del mayor número usado
- Al cerrar el período, los vouchers de una agencia se **consolidan** en un único recibo:
  - Leyenda con los números de voucher incluidos
  - Total = suma de importes de los vouchers

## Cobros extraordinarios

- Cobros extraordinarios independientes posibles (no relacionados con vouchers)

## Paleta visual

| Token | Valor |
| --- | --- |
| AccentColor | `#00695C` (verde azulado) |
| AccentLightColor | `#E0F2F1` |

## Flujo de emisión masiva

Resumen: Laura selecciona período → se consolidan los vouchers por agencia → cada recibo
obtiene CAE vía AFIP/ARCA WSFE → se arma un PDF único con el recibo + los vouchers
concatenados → se envía por mail. Bloqueo de duplicados por período.

Detalle completo del proceso, estados de la UI, mapeo del flag y plan de iteraciones en
**[cierre-periodo.md](cierre-periodo.md)**.

## Notas de crédito

- Para anular un recibo se genera una nota de crédito
- El recibo pasa a estado `Anulado`
