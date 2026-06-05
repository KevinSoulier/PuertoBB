# Cámara Portuaria — Lógica de negocio

## Descripción

La Cámara Portuaria emite recibos a **empresas** asociadas. Es administrada por Laura de forma unipersonal.

## Empresas y grupos

- ~29 empresas con cuota social mensual (mismo importe para todas)
- Grupos extraordinarios (~5 empresas con importe distinto al estándar)
- Los grupos pueden cambiar: se pueden agregar empresas o modificar integrantes
- Cobros extraordinarios puntuales posibles (ej. papelería)

## Paleta visual

| Token | Valor |
| --- | --- |
| AccentColor | `#1565C0` (azul marino) |
| AccentLightColor | `#E3F2FD` |

## Flujo de emisión masiva

1. Laura selecciona período (mes/año) y grupo
2. El sistema genera un recibo por cada empresa del grupo
3. Se bloquean duplicados: no se puede emitir dos veces el mismo recibo para la misma empresa en el mismo período
4. Cada recibo obtiene CAE via AFIP/ARCA WSFE
5. Se genera PDF y se envía por mail automáticamente al emitir

## Emisión individual

- Permite emitir un recibo a una empresa específica fuera del ciclo masivo
- Mismo flujo de CAE + PDF + mail

## Notas de crédito

- Para anular un recibo emitido se genera una nota de crédito
- El recibo pasa a estado `Anulado`
