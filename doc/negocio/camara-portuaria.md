# Cámara Portuaria — Lógica de negocio

## Descripción

La Cámara Portuaria emite recibos a **empresas** asociadas. Es administrada por Laura de forma unipersonal.

## Empresas

La unidad de negocio de Cámara Portuaria es la **Empresa**. Actualmente ~29 empresas asociadas.

## Grupos de facturación

Un **Grupo de Facturación** es una colección de Empresas a las que se les genera un recibo en bloque al cierre de un período elegido por Laura.

- Cada grupo tiene un importe de cuota propio (todas las empresas del grupo pagan lo mismo entre sí)
- La cantidad de empresas por grupo es variable y no está fijada en el sistema
- Los grupos son dinámicos: se pueden crear nuevos grupos, agregar o quitar empresas en cualquier momento
- Una misma empresa puede pertenecer a múltiples grupos (ej. cuota social + extraordinario)
- Cobros extraordinarios puntuales posibles para un grupo (ej. papelería)

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
