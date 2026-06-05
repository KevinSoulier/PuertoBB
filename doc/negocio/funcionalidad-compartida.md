# Funcionalidad compartida

Ambas entidades (Cámara Portuaria y Centro Marítimo) comparten la siguiente funcionalidad.

## ABM de empresas/agencias

- Alta, baja y modificación de empresas (Cámara) o agencias (Centro)
- Gestión de grupos de facturación (asignar/quitar empresas de grupos)

## Emisión de recibos

- **Masiva:** por período y grupo, con bloqueo de duplicados
- **Individual:** para una empresa/agencia fuera del ciclo masivo
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

## Dashboard de pendientes

- Vista de recibos en estado Emitido/Enviado/Vencido
- Alertas configurables

## Backup

- Backup manual del archivo SQLite desde la UI
- Un archivo SQLite por entidad

## Usuario

Laura administra ambas entidades de forma **unipersonal** desde la misma oficina. No hay múltiples usuarios ni roles.
