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

## Apoderado fiscal

- Posibilidad de facturar como persona apoderada
- El emisor fiscal es el apoderado
- El documento identifica al Centro Marítimo como receptor real

## Cobros extraordinarios

- Cobros extraordinarios independientes posibles (no relacionados con vouchers)

## Paleta visual

| Token | Valor |
| --- | --- |
| AccentColor | `#00695C` (verde azulado) |
| AccentLightColor | `#E0F2F1` |

## Flujo de emisión masiva

1. Laura selecciona período (mes/año)
2. El sistema consolida los vouchers por agencia en un único recibo
3. Bloqueo de duplicados por período
4. Cada recibo obtiene CAE via AFIP/ARCA WSFE
5. Se genera PDF y se envía por mail automáticamente

## Notas de crédito

- Para anular un recibo se genera una nota de crédito
- El recibo pasa a estado `Anulado`
