# Emisión masiva por grupo — tabla única y control por fila

> Aplica a ambas apps (CamaraPortuaria = Empresa, CentroMaritimo = Agencia). Rediseño del
> 2026-06-09. Ver decisión [D-19](../decisiones/registro-decisiones.md).

## Qué muestra la pantalla

Una **sola tabla** con una fila por miembro del grupo seleccionado, para el período elegido:

| Columna | Contenido |
| --- | --- |
| Empresa / Agencia | Nombre del miembro |
| Comprobante | `PdV-Número` si tiene CAE, `—` si no |
| Importe | Total del recibo (suma de líneas), `—` si no emitido |
| **Estado** | Badge: `No emitido` (gris) si aún no hay recibo; si lo hay, el estado real (`Pendiente`, `Emitido`, `Enviado`, `Pagado`, `Vencido`, `Anulado`) calculado por `EstadoReciboHelper` |
| Estado envío | `CAE pendiente` / `Sin enviar` / `Mail falló` / `Enviado`; el tooltip muestra el último error |
| Acciones | Íconos por fila: **Emitir**, **Enviar**, **Ver PDF** |

Al cambiar grupo, mes o año se recarga el estado desde la base (`GetEstadoMasivoAsync`), así que
lo emitido en períodos anteriores se ve reflejado al volver. Antes había **dos** tablas (una previa
y otra de resultados con columnas separadas Éxito / Error emisión / Error mail); se unificaron en esta.

## Acciones

La emisión es un proceso de **dos pasos**: obtener el CAE (emisión) y enviar el mail. Cada paso tiene
su botón, habilitado sólo cuando ese paso está pendiente o falló, y deshabilitado cuando ya está hecho
(re-clickear = reintentar). No hay un botón "reintentar" aparte.

**Masivas (barra superior, 3 botones):**
- **Emitir** → `EmitirMasivoAsync(enviarMail: false)`: obtiene el CAE de las pendientes, sin mandar mail.
- **Enviar** → `EnviarMasivoAsync`: manda el mail de las que ya tienen CAE y aún no se enviaron.
- **Emitir y enviar** → `EmitirMasivoAsync(enviarMail: true)`: ambos pasos en una pasada.

**Por fila (entidad seleccionada):**
- **Emitir** (`EsEmitible` = sin recibo, o recibo `Pendiente`): si no hay recibo, `EmitirDeGrupoAsync`; si ya existe, `ReintentarAsync`. Siempre sin mail (el mail lo cubre Enviar).
- **Enviar** (`EsEnviable` = hay CAE y mail pendiente/fallido, o reenvío con confirmación): `ReenviarMailAsync`.
- **Ver PDF** (`EsPrevisualizable` = hay CAE): `GenerarPdfReciboAsync` + `ShowPdfAsync`.

La misma lógica de "el botón de emisión/CAE se habilita sólo si falta el CAE" se aplicó al botón de
reintento de la pantalla **Recibos** (`ReciboItem.EsReintentable == EstadoPersistido == Pendiente`); el
reenvío de mail allí lo cubre el botón de correo.

## Modelo de datos

- **Servicio → UI:** `EstadoEmisionEntidad<TRecibo>(EntidadId, EntidadNombre, Recibo?)` (Core/Models/Resultados).
  `Recibo` null = la entidad no fue emitida en el período. `GetEstadoMasivoAsync` cruza los miembros del
  grupo (`GetConMiembrosAsync`) con `GetPorGrupoYPeriodoAsync`.
- **Fila de la grilla:** `EmisionMasivaItem` (por app, en `ViewModels/Items`), proyección superset de
  `ReciboItem` que además contempla el caso "sin recibo". Expone `EsEmitible`/`EsEnviable`/`EsPrevisualizable`.
- **Ítems del recibo:** salen de las líneas del grupo (`GrupoFacturacion.Lineas`); fallback a línea única
  para grupos legacy sin líneas. El detalle se persiste como snapshot (`Recibo.Lineas`) y el PDF se arma
  siempre desde ahí. Mientras el recibo no tenga CAE (`Pendiente`), reintentarlo re-sincroniza el snapshot
  con los ítems actuales del grupo; con CAE el detalle queda congelado por integridad fiscal.

## Archivos

- ViewModel: `*/ViewModels/EmisionMasivaViewModel.cs`
- Item: `*/ViewModels/Items/EmisionMasivaItem.cs`
- Vista: `*/Views/EmisionMasivaPage.xaml(.cs)`
- Servicio: `PuertoBB.Services/Negocio/*ReciboService.cs` (`GetEstadoMasivoAsync`, `EmitirMasivoAsync(enviarMail)`, `EnviarMasivoAsync`, `EmitirDeGrupoAsync`)
- Repo: `*/ReciboRepository.cs` (`GetPorGrupoYPeriodoAsync`; `GetPorClaveAsync` ahora incluye `Lineas`)
- Estados/colores: `PuertoBB.Core/Common/EstadoReciboHelper.cs`, `*/Converters/EstadoReciboToBrushConverter.cs` (caso `No emitido`)
