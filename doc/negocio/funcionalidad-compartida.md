# Funcionalidad compartida

Ambas entidades (Cámara Portuaria y Centro Marítimo) comparten la siguiente funcionalidad.

## Terminología por aplicación

| Concepto | Cámara Portuaria | Centro Marítimo |
| --- | --- | --- |
| Unidad de negocio | **Empresa** | **Agencia** |
| Conjunto para emisión masiva | **Grupo de Facturación** | **Grupo de Facturación** |

Aunque las dos aplicaciones comparten la misma arquitectura, los términos de dominio difieren. El código usa los nombres en español de cada contexto (`Empresa` vs `Agencia`), nunca un nombre genérico.

## Grupos de facturación

El **Grupo de Facturación** es el concepto central de la emisión masiva:

- Agrupa Empresas (Cámara) o Agencias (Centro) bajo un importe de cuota común
- Laura selecciona el período y el grupo; el sistema genera un recibo por cada entidad del grupo
- Los grupos son dinámicos: cantidad variable de miembros, modificables en cualquier momento
- Una entidad puede pertenecer a más de un grupo (ej. cuota social + grupo extraordinario)
- Bloqueo de duplicados: no se emite dos veces el mismo comprobante para la misma entidad en el mismo período

## ABM de empresas/agencias

- Alta, baja y modificación de Empresas (Cámara) o Agencias (Centro)
- ABM de Grupos de Facturación: crear grupos, asignar/quitar integrantes, definir importe

## Emisión de recibos

- **Masiva:** Laura elige período y grupo → el sistema genera un recibo por cada miembro
- **Individual:** emitir un recibo a una Empresa/Agencia específica fuera del ciclo masivo
- **Nota de crédito:** para anular un recibo emitido

## Estados de recibo

| Estado | Color de fondo |
| --- | --- |
| Emitido | `#E3F2FD` (azul claro) |
| Enviado | `#FFF9C4` (amarillo claro) |
| Pagado | `#E8F5E9` (verde claro) |
| Vencido | `#FFEBEE` (rojo claro) |
| Anulado | `#F5F5F5` (gris claro) |

## Integración AFIP/ARCA

- Webservice WSFE (SOAP) para obtener CAE
- Autenticación via WSAA (ticket de acceso)
- Errores de AFIP se representan como `AfipException`

## PDF

- Generado con QuestPDF al emitir cada recibo
- Enviado automáticamente por mail (MailKit) al momento de la emisión

## Control de pagos

- Laura marca manualmente un recibo como **Pagado** desde la vista de recibos o el dashboard
- Al marcar como pagado se registra la `FechaPago`
- `FechaVencimientoPago` = `FechaEmision` + `DiasVencimiento` (configurable, ej. 30 días)
- **"Vencido" no es un estado persistido** — se calcula visualmente: recibos con `FechaVencimientoPago < hoy` y estado Emitido/Enviado se destacan en rojo en el dashboard

## Dashboard de pendientes

- Vista filtrable de recibos en estado Emitido / Enviado (+ los visualmente vencidos)
- Filtros: por período (mes/año), por grupo/generación, por empresa/agencia
- Columnas: Empresa/Agencia, Período, Importe, Estado, FechaVencimientoPago, Días de atraso
- Días de atraso calculado en tiempo de presentación, no persistido
- Solo vista en pantalla, sin exportación

## Backup

- Backup manual del archivo SQLite desde la UI
- Un archivo SQLite por entidad

## Usuario

Laura administra ambas entidades de forma **unipersonal** desde la misma oficina. No hay múltiples usuarios ni roles.
