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

- Agrupa Empresas (Cámara) o Agencias (Centro) y define los **ítems a facturar** (`ReciboLinea`: descripción + cantidad + precio unitario). El total del grupo es la suma de sus ítems.
- Esos ítems son copiados al `Recibo.Lineas` al emitir (snapshot inmutable por recibo). El panel de edición del grupo sólo se habilita al crear o seleccionar un grupo (`EnEdicion`), y "Guardar" requiere nombre + al menos un ítem.
- Laura selecciona el período y el grupo; el sistema genera un recibo por cada entidad del grupo.
- Los grupos son dinámicos: cantidad variable de miembros, modificables en cualquier momento.
- Una entidad puede pertenecer a más de un grupo (ej. cuota social + grupo extraordinario).
- Bloqueo de duplicados: la relación `EmisionGrupo` (índice único sobre GrupoId+EntidadId+Período) impide emitir dos veces el mismo comprobante por grupo; los individuales no tienen esta restricción.

## ABM de empresas/agencias

- Alta, baja y modificación de Empresas (Cámara) o Agencias (Centro).
- CUIT validado con dígito verificador (pesos 5,4,3,2,7,6,5,4,3,2) al guardar.
- `EsMoroso` es un flag manual que se muestra en la lista; no bloquea la emisión.
- ABM de Grupos de Facturación: crear grupos, asignar/quitar integrantes, definir ítems.

## Emisión de recibos

- **Masiva:** Laura elige período y grupo → ve **una sola tabla** con una fila por miembro y su estado para ese período ("No emitido" si todavía no se emitió). Tres acciones masivas — **Emitir** (obtener CAE), **Enviar** (mail de los ya emitidos) y **Emitir y enviar** — más acciones por fila (Emitir/reintentar, Enviar, Ver PDF). Detalle de diseño en `doc/diseño/emision-masiva.md`.
- **Individual:** emitir un recibo a una Empresa/Agencia específica fuera del ciclo masivo. Se permiten N recibos individuales por (entidad, período).
- **Nota de crédito:** para anular un recibo emitido. El PDF incluye sección "Comprobante asociado" con el tipo y número del recibo original.

## Modelo de Recibo (resumen)

- `Recibo` es entidad de auditoría autocontenida: los datos fiscales del receptor se copian al emitir en campos `Receptor*` (Nombre, RazonSocial, Cuit, Domicilio, CondicionIva). El PDF y AFIP leen del snapshot, nunca navegan a la entidad original.
- El vínculo con el grupo que originó el recibo vive en `EmisionGrupo` (cascade al borrar el grupo; el recibo sobrevive). `Recibo` no tiene `GrupoFacturacionId` directamente.
- El detalle del comprobante son las `Lineas` (`ReciboLinea`): snapshot inmutable copiado al emitir. `Recibo.Detalle` es solo un encabezado/leyenda opcional.
- La emisión es idempotente: el recibo se persiste en estado `Pendiente` antes de pedir el CAE a AFIP. Si falla, queda `Pendiente` con `UltimoErrorCae` y puede reintentarse.

## Estados de recibo

| Estado | Color de fondo | Significado |
| --- | --- | --- |
| Pendiente | `#FFF3E0` (naranja claro) | Creado; CAE pendiente (reintentable) |
| Emitido | `#E3F2FD` (azul claro) | CAE obtenido; mail no enviado aún |
| Enviado | `#FFF9C4` (amarillo claro) | Mail enviado exitosamente |
| Pagado | `#E8F5E9` (verde claro) | Laura lo marcó como cobrado |
| Vencido | `#FFEBEE` (rojo claro) | Calculado en presentación (no persistido) |
| Anulado | `#F5F5F5` (gris claro) | Anulado con NC |

"Vencido" no es un estado persistido: se calcula visualmente cuando `FechaVencimientoPago < hoy` y el estado no es `Pagado`/`Anulado`.

## Integración AFIP/ARCA

- Librería `Afip.Net` (proyecto independiente): WSFE (SOAP) para CAE + WSAA para tickets.
- Configuración por `PuntoDeVenta` (entidad propia): número, ambiente (homologación/producción), certificado PKCS#12 o PEM+KEY. Solo un PV puede estar activo; la app usa el activo al emitir.
- `CertificadoPassword` cifrado en reposo (DPAPI, prefijo `dpapi:`).
- Cache de ticket WSAA por servicio, persistido en disco cifrado.
- Botón "Probar conexión" en Configuración: valida servicio + autenticación + último comprobante.
- Errores de AFIP descritos con `AfipErrores.Describir` (traduce códigos numéricos a texto).

## PDF

- Generado con QuestPDF a demanda (no se persiste en DB) usando `Afip.Documentos`.
- El emisor del PDF se construye desde `AfipConfig` (que incluye `IngresosBrutos` e `InicioActividades`).
- Enviado automáticamente por mail (MailKit) al momento de la emisión.

## Control de pagos

- Laura marca manualmente un recibo como **Pagado** desde la vista de recibos o el dashboard.
- Al marcar como pagado se registra la `FechaPago`.
- `FechaVencimientoPago` = `FechaEmision` + `DiasVencimiento` (configurable, ej. 30 días).
- **"Vencido" no es un estado persistido** — se calcula visualmente.

## Dashboard de pendientes

- Vista filtrable de recibos en estado Pendiente / Emitido / Enviado (+ visualmente vencidos).
- Filtros: por período (mes/año), solo vencidos, por empresa/agencia.
- Columnas: Empresa/Agencia, Período, Importe, Estado, FechaVencimientoPago, Días de atraso.
- Días de atraso calculado en tiempo de presentación, no persistido.

## Backup

- Backup manual del archivo SQLite desde la UI (VACUUM INTO a un archivo de destino).
- Restaurar: cierra conexiones (SqliteConnection.ClearAllPools), copia el archivo, pide reiniciar la app.

## Usuario

Laura administra ambas entidades de forma **unipersonal** desde la misma oficina. No hay múltiples usuarios ni roles.
