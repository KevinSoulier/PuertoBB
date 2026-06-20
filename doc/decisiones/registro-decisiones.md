# Registro de decisiones de diseño — Implementación end-to-end

> Documento vivo. Registra cada decisión tomada durante la implementación autónoma
> de la plataforma PuertoBB, con su motivo. Complementa (no reemplaza) las decisiones
> ya registradas en `doc/arquitectura/datos.md`.

Fecha de inicio de la sesión de implementación: 2026-06-05.

---

## D-01 — Framework MVVM: hand-rolled, no CommunityToolkit

**Decisión:** Se usa el `BaseViewModel` + `RelayCommand` escritos a mano (ya presentes en
el repo y descritos en `doc/arquitectura/convenciones.md`). Se agrega un `AsyncRelayCommand`
para comandos asíncronos con soporte de `IsBusy`/`CanExecute`.

**Motivo:** `convenciones.md` es el documento autoritativo de convenciones y ya fija el
patrón hand-rolled, con código existente en ambos proyectos UI. `fluent-navigation.md`
(estado "investigado, pendiente") sugería `CommunityToolkit.Mvvm`. Ante el conflicto se
prioriza la convención establecida para no introducir una dependencia ni dos estilos de
ViewModel en la misma base de código. Cero dependencias externas de UI.

---

## D-02 — AFIP: lógica real + implementación Fake conmutable

**Decisión:** Se implementa toda la orquestación WSAA (TRA → CMS PKCS#7 → caché de ticket)
y WSFE (mapeo de comprobante → request de CAE) contra una **abstracción de cliente SOAP**
(`IWsaaClient` / `IWsfeClient`). Para correr sin red ni certificado se provee
`FakeAfipService` (CAE simulado determinístico). La selección real/fake se hace por
configuración (`AfipUsarHomologacion` / disponibilidad de certificado) en el arranque DI.

**Motivo:** La generación del cliente SOAP real desde el WSDL (`dotnet-svcutil`) requiere
acceso de red al endpoint de AFIP y un certificado `.p12` válido — no disponibles en la
sesión autónoma. Abstrayendo el cliente, toda la lógica de negocio AFIP queda implementada,
testeable y lista; sólo resta generar el cliente concreto (paso manual documentado en
`doc/arquitectura/afip-integracion.md`). El `FakeAfipService` permite desarrollar y testear
el sistema completo de punta a punta.

---

## D-03 — Testing: xUnit + NSubstitute + SQLite in-memory

**Decisión:** Proyecto `PuertoBB.Tests` (net10.0) con xUnit como runner, NSubstitute para
dobles de prueba de interfaces, y EF Core SQLite in-memory (conexión `:memory:` mantenida
abierta) para tests de repositorios reales.

**Motivo:** xUnit es el estándar de facto en .NET moderno. NSubstitute da una sintaxis de
mocking limpia sin lambdas verbosas. SQLite in-memory ejerce el mapeo EF real (índices
únicos, relaciones) sin tocar disco — más fiel que el provider InMemory de EF.

---

## D-04 — Servicios de negocio por aplicación

**Decisión:** Las interfaces de servicio de negocio que difieren entre apps se dividen
(`ICamaraPortuariaReciboService` / `ICentroMaritimoReciboService`, y los PDF ya divididos).
La lógica común (AFIP, Mail) se comparte vía interfaces únicas.

**Motivo:** El flujo de emisión difiere (CP: cuota por grupo / individual; CM: además
consolidación de vouchers y apoderado fiscal). Contratos separados evitan métodos vacíos y
mantienen cada servicio expresivo, consistente con la decisión ya tomada para PDF en
`doc/arquitectura/flujos.md`.

---

## D-05 — "Vencido" calculado en presentación

**Decisión:** Se respeta `datos.md`: `ReciboEstado` no incluye `Vencido`. El estado vencido
es un cálculo de presentación (`FechaVencimientoPago < hoy && Estado ∈ {Emitido, Enviado}`),
encapsulado en un helper compartido para no duplicarlo entre ViewModels.

---

## D-06 — Numeración de comprobantes AFIP

**Decisión:** El número de comprobante se obtiene de AFIP vía `FECompUltimoAutorizado + 1`
justo antes de solicitar el CAE, dentro del servicio AFIP (no se persiste un contador propio
de recibos). El `ContadorVoucher` sí es propio (los vouchers no son comprobantes AFIP).

**Motivo:** AFIP es la fuente de verdad de la numeración fiscal; mantener un contador paralelo
arriesga desincronización. El `FakeAfipService` simula esta secuencia en memoria.

---

## D-07 — Mocks/seed de datos para correr sin backend real

**Decisión:** Se incluye `SeedData` opcional (activable por configuración de desarrollo) que
puebla empresas/agencias/grupos/barcos de ejemplo, y el `FakeAfipService` + un `FakeMailService`
para correr la app completa sin AFIP ni SMTP reales.

**Motivo:** Permite validar la plataforma de punta a punta (incluido el recorrido visual WPF)
sin credenciales externas, cumpliendo el pedido de "mocks donde sea necesario para testear".

---

## D-08 — Ciclos de vida DI en WPF: Transient para datos/servicios

**Decisión:** `DbContext`, repositorios, servicios de negocio, providers de config, AFIP/Mail reales
y ViewModels/Páginas se registran **Transient**. Singletons: `INavigationService`, `IDialogService`,
`MainWindow`/`MainWindowViewModel`, PDF, `WsaaTokenCache` y los fakes (AFIP/Mail).

**Motivo:** WPF no tiene scope por request. Un `DbContext` Scoped resuelto desde el root vive como
singleton y arrastra tracking obsoleto entre páginas. Transient da contextos de vida corta por
operación (la app es unipersonal y secuencial), evitando ese problema. `WsaaTokenCache` es Singleton
para compartir el ticket de 12 hs; los fakes son Singleton para mantener su contador en memoria.

---

## D-09 — Diálogos modales por overlay (no MessageBox)

**Decisión:** `IDialogService` se implementa mostrando `UserControl`s (`ConfirmDialog`, `AlertDialog`,
`InputDialog`) en un overlay con `ContentPresenter` en `MainWindow`, resueltos vía `TaskCompletionSource`.

**Motivo:** WPF no tiene `ContentDialog`. Cumple la regla "nunca `MessageBox`" de `ux-reglas.md` y usa
brushes dinámicos del tema Fluent (se adaptan a claro/oscuro). El `MessageBox` solo sobrevive como
último recurso si el shell aún no se inicializó (handler global de excepciones).

---

## D-10 — Persistencia de tema por archivo, no Settings

**Decisión:** La preferencia de tema (claro/oscuro/sistema) se guarda en un `tema.txt` en
`%LocalAppData%\PuertoBB\<App>` (`PreferenciasUsuario`), no en `Properties.Settings`.

**Motivo:** Evita el andamiaje de `Settings` en proyectos SDK-style; una sola preferencia simple no lo
justifica. Se restaura en `App.OnStartup` antes de mostrar la ventana (sin parpadeo de tema).

---

## D-11 — `WPF0001` (ThemeMode) suprimido

**Decisión:** `System.Windows.ThemeMode` / `Application.ThemeMode` son experimentales en .NET 10
(diagnóstico `WPF0001`). Se suprime con `<NoWarn>WPF0001</NoWarn>` en los dos `.csproj` de UI.

**Motivo:** Es la API nativa de tema documentada en `diseño-wpf`; es estable para nuestro uso y evita
una dependencia de terceros para dark mode. Se acepta el riesgo de cambio futuro de API.

---

## D-12 — Bug corregido: entidades *detached* en el cierre de período

**Decisión / hallazgo:** En `CerrarPeriodoAsync` se cargaba la agencia y los vouchers con `AsNoTracking`
(detached) y se asignaban a las navegaciones de un `Recibo` nuevo antes de `AddAsync`. EF los trataba
como entidades nuevas e intentaba reinsertarlos, violando los índices únicos (Numero de voucher / Cuit).
**Corrección:** cargar la agencia rastreada (`GetConDetalleAsync`) y pasar los vouchers solo como dato
para el PDF/consolidación (sin asignarlos a la navegación persistida).

**Motivo / lección:** Detectado por el test `CerrarPeriodo_ConsolidaVouchers` antes de llegar a
producción. Regla general anotada en el skill `/testing`: nunca asignar entidades detached a las
navegaciones de una entidad nueva; usar la versión rastreada o solo el FK id.

---

## D-13 — Cliente AFIP real: generado con svcutil, en `PuertoBB.Services`

**Decisión:** Los clientes SOAP de WSAA (`LoginCms`) y WSFE v1 se generaron con `dotnet-svcutil`
desde los WSDL de homologación, en `PuertoBB.Services/Afip/Soap/Generated/`. Las implementaciones
`WsaaSoapClient`/`WsfeSoapClient` (+ `WsfeMapper`) cubren `IWsaaClient`/`IWsfeClient`. Paquetes
`System.ServiceModel.Http` + `System.ServiceModel.Primitives` (4.10.*).

**Por qué en Services y no en Infrastructure:** el README asigna AFIP a `PuertoBB.Services`, y allí
viven `AfipService` y las abstracciones `IWsaaClient`/`IWsfeClient`. Una implementación concreta debe
poder referenciar esas interfaces; como `Infrastructure` no referencia `Services` (regla de
dependencias), el hogar natural del cliente es `Services`. Esto ajusta la nota original de
`afip-integracion.md` (que sugería Infrastructure, previa a la abstracción introducida en D-02).

**Referencia:** `~/source/repos/FacturadorAfip/FacturadorAfip.AfipWsfeClient` (cliente AFIP real del
equipo) — confirmó el patrón de `ServiceSoapClient`/`LoginCMSClient`, `FEAuthRequest`, `FECAERequest`.

**Pendiente real (no testeable sin credenciales):** generar el cliente desde el WSDL de **producción**
si se desea, y cargar el certificado `.p12` + punto de venta habilitado. El mapeo y la firma están
testeados; la llamada de red end-to-end requiere certificado válido.

---

## D-14 — Adopción de WPF-UI (NavigationView + FluentWindow + Mica)

**Decisión:** Se reemplaza el Fluent **nativo** de .NET 10 (`PresentationFramework.Fluent` +
`Application.ThemeMode`) por la librería **WPF-UI 4.3.0** (NuGet `WPF-UI` + `WPF-UI.DependencyInjection`)
en ambas apps. El shell pasa a `ui:FluentWindow` con `ui:TitleBar` + `ui:NavigationView`; los iconos de
navegación usan `ui:SymbolIcon` (Fluent System Icons); la transparencia es **Mica** real
(`WindowBackdropType="Mica"` + `ExtendsContentIntoTitleBar`). El tema se gestiona con
`ApplicationThemeManager` + `SystemThemeWatcher` (en lugar de `ThemeMode`), manteniendo la persistencia
en `tema.txt` (D-10). El **acento es el del sistema** (Windows): no se fuerza color de marca, se deja
`updateAccent: true` y los botones/indicadores siguen el acento de Windows vía `AccentFillColorDefault`.

**Motivo:** El `NavigationView` de WPF-UI (indicador de selección animado, pane header/footer, back
integrado) es superior al `TreeView`+`Frame` hecho a mano, y la librería entrega la transparencia Mica
y el set de iconos tipados que se pedían, con alta fidelidad al diseño de referencia (repo `wpfui`).
La migración fue acotada porque WPF-UI usa **los mismos resource keys WinUI** que el Fluent nativo
(`TextFillColor*`, `ApplicationBackgroundBrush`, `Card*`, `SystemFillColor*`, etc.), así que las páginas
no requirieron cambios de brushes. Se eliminó el `RuntimeHostConfigurationOption EnableMicaBackdrop` y el
`NoWarn WPF0001` (revierte parcialmente D-11: ya no se usa `ThemeMode`).

**Consecuencias / detalles:**
- Se borró la navegación propia (`Navigation/INavigationService`, `Navigation/NavigationService`,
  `Models/NavigationItem`); se usa `Wpf.Ui.INavigationService` + `AddNavigationViewPageProvider()`.
- WPF-UI no define `AccentButtonStyle` (sí lo hacía el Fluent nativo). Se replicó en `Resources/Styles.xaml`
  con un template propio sobre los brushes `AccentButton*` de WPF-UI, para no tocar páginas ni diálogos.
- Los diálogos por overlay (D-09) se mantienen sin cambios (usan los mismos brushes del tema).
- Mejores prácticas extraídas del repo de referencia documentadas en `doc/diseño/fluent-wpfui.md`
  (supera a `doc/diseño/fluent-navigation.md`).
</content>

---

## D-15 — Rediseño de Cierre de Período: UI agrupada por agencia + descarga PDF (PdfSharp)

**Fecha:** 2026-06-06.

**Decisión:** Se rediseña la página de **Cierre de período** del Centro Marítimo: la
grilla pasa a mostrar una fila por agencia (con sus vouchers expandibles en `RowDetails`),
un flag de tres estados (`Pendiente` / `Emitido` / `Completo`) derivado de `Recibo.Estado`,
y un botón **Descargar PDF** adaptativo por agencia. La descarga concatena el PDF del
recibo (con CAE+QR) con los PDFs individuales de los vouchers cuando el recibo ya está
emitido, o sólo los vouchers cuando todavía está pendiente.

**Cómo se implementa:**

- `IVoucherRepository.GetTodosByPeriodoAsync` (nuevo) trae todos los vouchers del período
  con `Agencia`, `Barco` y `Recibo` incluidos.
- `IVoucherService.GetCierrePeriodoAsync` arma el shape de la vista (`AgenciaCierrePeriodoVm`)
  agrupando por agencia y derivando el estado del recibo consolidado.
- `IPdfMerger` (nueva) + `PdfMerger` (PdfSharp 6.2.0) concatenan PDFs.
- `ICentroMaritimoPdfService.GenerarPdfDescargaAsync` arma el blob final (recibo opcional +
  vouchers).
- `IVoucherRepository.GetByIdConDetalleAsync` + comandos `DescargarVoucherCommand` y
  `PrevisualizarVoucherCommand` en `VouchersViewModel` permiten descargar/previsualizar
  un voucher individual desde la página de Vouchers.

**Por qué PdfSharp y no iText:** QuestPDF (ya en uso) no concatena PDFs. PdfSharp es MIT,
sin dependencias nativas, mantenido, y soporta net10.0. iText 7+ es AGPL/comercial — no
encaja con la licencia del proyecto.

**Alcance restringido a esta iteración:** la emisión por agencia (botón "Generar recibo") y
el cierre masivo quedan visibles pero deshabilitados — se habilitan en iteración 2.
El envío automático por mail del PDF único entra en iteración 3.

**Por qué fasear así:** la descarga manual ya cubre el caso en que Laura necesita revisar
los comprobantes antes de cerrar el período o reenviar el adjunto fuera de banda; la
emisión automatizada implica cambios en `CentroMaritimoReciboService` que tienen su propio
costo y prefieren su propio PR.

**Documentación:** el proceso completo (4 fases, mapeo de estados, plan de iteraciones,
aclaración sobre AFIP no devolviendo PDF) queda en `doc/negocio/cierre-periodo.md`, y
`doc/negocio/centro-maritimo.md` ahora linkea ahí.

---

## D-16 — AFIP extraído a librería neutra `Afip.Net` (multi-servicio) + adaptador en Services

**Fecha:** 2026-06-06.

**Decisión:** Todo el cliente AFIP se movió de `PuertoBB.Services/Afip/` a un **proyecto independiente
y reutilizable `Afip.Net`** (namespace raíz `Afip`, sin dependencias de PuertoBB). El dominio sigue
hablando con `IAfipService` (en Core); un **adaptador** `PuertoBB.Services/Afip/AfipService.cs` traduce
los modelos del dominio ↔ los de la librería y llama a la fachada `IWsfeService` de `Afip.Net`. Esto
revisa la ubicación fijada en D-13 (que dejaba el cliente concreto dentro de `PuertoBB.Services`).

**Diseño multi-servicio:** la autenticación WSAA quedó **compartida por todos los servicios** vía
`ITicketProvider.GetTicketAsync(servicio, options)`. Sumar un web service nuevo (ej. **Remito
Electrónico** `wsrem*`) es agregar su cliente SOAP + fachada reutilizando el `ITicketProvider`; el
`TicketCache` ya está keyed por `(CUIT, servicio)`. Placeholder documentado en `Afip.Net/Wsrem/README.md`.

**Mejoras de robustez incluidas:**
- Cache del TA **persistido y cifrado a disco** con DPAPI (`FileTicketStore`) → sobrevive reinicios y
  evita el rechazo "El CEE ya posee un TA válido". Reemplaza al `WsaaTokenCache` en memoria (que se borró).
- **Contraseña del certificado cifrada en reposo** (DPAPI, `ISecretProtector` en `PuertoBB.Services/Security`),
  con migración suave de valores en texto plano.
- **Importación del `.p12`** a una carpeta controlada de la app al seleccionarlo.
- `uniqueId` del TRA **monótono** (evita colisiones en el mismo segundo).
- **Botón "Probar conexión"** en Configuración (login WSAA + FEDummy + último número) y **aviso de
  ambiente** homologación/producción.
- NU1903: se fija `System.Security.Cryptography.Pkcs` 10.0.0 para sacar la transitiva vulnerable (6.0.1).

**Por qué:** el usuario pidió que AFIP fuera un proyecto/servicio aparte, genérico y reutilizable a
futuro para otras cosas, y que todo siguiera siendo configurable desde la app. El patrón adaptador deja
`Core` sin dependencias y `Afip.Net` 100% reutilizable.

**Docs:** `Afip.Net/README.md` (desarrollador), `doc/usuario/afip-configuracion.md` (usuario/certificados),
`doc/arquitectura/afip-integracion.md` (actualizado).

**Pendiente real:** prueba end-to-end con certificado válido (igual que en D-13). Mapeo WSFE, firma TRA,
cache por servicio y persistencia cifrada quedan cubiertos por tests.

---

## D-17 — Múltiples puntos de venta con uno activo (perfil por ambiente)

**Fecha:** 2026-06-06.

**Decisión:** La configuración deja de tener un único punto de venta + certificado + ambiente. Se agrega
la entidad **`PuntoDeVenta`** (por app: CamaraPortuaria y CentroMaritimo) con `Nombre`, `Numero`,
`UsarHomologacion`, `CertificadoRuta`, `CertificadoPassword` y `Activo`. `Configuracion` ahora tiene
`List<PuntoDeVenta> PuntosDeVenta` + `PuntoDeVentaActivo` (`[NotMapped]`). Se quitaron de `Configuracion`
los campos `PuntoDeVenta` (int), `AfipCertificadoRuta`, `AfipCertificadoPassword`, `AfipUsarHomologacion`.

**Nombre:** el usuario pidió llamarlo **"Punto de venta"** (término oficial de AFIP) aunque el registro
agrupe número + ambiente + certificado. En la práctica cada punto de venta = un ambiente (ej. uno de
homologación y otro de producción) y se elige cuál está **activo** con un click.

**Cómo se implementa:**
- `AfipConfigProvider` (ambas UIs) arma el `AfipConfig` a partir del **PV activo** (cert + ambiente) + CUIT.
- Los servicios de negocio usan `config.PuntoDeVentaActivo?.Numero` para el comprobante.
- `IConfiguracionRepository` suma `GetPuntosDeVentaAsync`, `GuardarPuntoDeVentaAsync`,
  `EliminarPuntoDeVentaAsync` y `MarcarPuntoDeVentaActivoAsync` (deja exactamente uno activo).
- La contraseña de cada PV se cifra con DPAPI (igual que antes, ahora por punto de venta).
- Migración `AgregarPuntosDeVenta` (ambos contextos): dropea las 4 columnas viejas, crea la tabla
  `PuntosDeVenta` (FK a Configuracion) y siembra un PV "Principal" activo por defecto.
- UI Configuración: grilla de puntos de venta + alta/edición/baja + "Marcar activo" + "Probar conexión"
  (prueba el activo). Tests nuevos: seed con activo, marcar activo único, eliminar.

**Por qué:** permite tener homologación y producción cargados a la vez y alternar sin recargar el
certificado ni reescribir el número de PV cada vez.

---

## D-18 — Backup/Restaurar + Mantenimiento SQLite en tab "Base de datos"

**Fecha:** 2026-06-07.

**Decisión:** Se amplía el tab "Base de datos" en `ConfiguracionPage` de ambas apps (CentroMaritimo y
CamaraPortuaria) con cuatro operaciones nuevas sobre la base SQLite, distribuidas en dos secciones:

**Sección Backup:**
- **Generar backup** (ya existía): usa `VACUUM INTO` para exportar una copia consistente sin bloquear la DB.
  Se corrigió el ícono (`DocumentArrowDown24` → `ArrowDownload24`): el símbolo original no existe en el
  font embebido de WPF-UI 4.3.0 y aparecía como glifo vacío.
- **Restaurar backup** (nuevo): abre un `OpenFileDialog`, pide confirmación explícita con aviso de
  cierre, cierra la conexión EF Core (`_db.Database.CloseConnection()`), copia el archivo `.db`
  seleccionado sobre la base activa con `File.Copy(origen, dbPath, overwrite: true)`, y llama
  `Application.Current.Shutdown()`. La ruta de la base se obtiene casteando la conexión a
  `SqliteConnection` y leyendo `DataSource`.

**Sección Mantenimiento:**
- **Verificar integridad**: ejecuta `PRAGMA integrity_check` vía ADO.NET directo (EF Core no retorna
  filas de PRAGMAs con `ExecuteSqlRaw`). Si SQLite responde `"ok"` muestra un mensaje de éxito; si
  detecta problemas, los muestra en un `AlertDialog`.
- **Compactar (VACUUM)**: ejecuta `VACUUM` que reconstruye el archivo de la base, recupera espacio
  en disco, y rehace todos los índices (equivale a REINDEX implícito).
- **Optimizar consultas**: ejecuta `PRAGMA optimize` para actualizar las estadísticas internas del
  query planner de SQLite.

**Implementación:**
- `IBackupService` (Core): se agregaron `RestaurarAsync`, `VerificarIntegridadAsync`, `VacuumAsync`,
  `OptimizarAsync`. Todas retornan `ServiceResult<T>` como el resto del servicio.
- `BackupService` (ambas UIs): implementa los cuatro métodos nuevos + `using System.IO` +
  `using Microsoft.Data.Sqlite` (para `SqliteConnection.DataSource`).
- `ConfiguracionViewModel` (ambas UIs): cuatro comandos `AsyncRelayCommand` nuevos con sus métodos
  privados. El restore usa `_dialog.ShowConfirmAsync` para la advertencia destructiva.
- `ConfiguracionPage.xaml` (ambas UIs): el tab queda con dos subsecciones visuales (separadas por
  `Separator`) con títulos, descripción secundaria en gris, y botones con íconos Fluent.

**Por qué estas operaciones:**
- `VACUUM` y `PRAGMA optimize` son las dos herramientas de mantenimiento preventivo recomendadas por
  SQLite para bases que crecen con el tiempo. `PRAGMA integrity_check` es el primer paso ante cualquier
  comportamiento inesperado o fallo de energía. El restore completa el ciclo backup/restore que ya existía
  solo en una dirección.

**Docs:** `doc/usuario/base-de-datos.md`.

---

## D-19 — Emisión masiva: tabla única, acciones por fila y fix de ítems del grupo

**Fecha:** 2026-06-09.

**Decisión:** Se rediseña la pantalla de **Emisión masiva por grupo** (ambas apps). Las dos tablas
previas (previa + resultados con columnas Éxito / Error emisión / Error mail) se reemplazan por **una
sola tabla** con una fila por miembro del grupo y su **estado para el período** (badge, reutilizando
`EstadoReciboHelper` + `EstadoReciboToBrushConverter`, con un caso nuevo `No emitido`). La emisión se
modela como dos pasos con un botón cada uno, habilitado sólo cuando ese paso falta/falló y deshabilitado
cuando ya está hecho (re-clickear = reintentar):

- **Masivas (3 botones):** Emitir (`EmitirMasivoAsync(enviarMail:false)`), Enviar (`EnviarMasivoAsync`),
  Emitir y enviar (`EmitirMasivoAsync(enviarMail:true)`).
- **Por fila:** Emitir/reintentar (`EmitirDeGrupoAsync` si no hay recibo, `ReintentarAsync` si existe),
  Enviar (`ReenviarMailAsync`), Ver PDF.

La misma idea se aplicó al botón de reintento de **Recibos**: `EsReintentable` pasa a ser sólo "paso CAE
pendiente" (`Pendiente`), dejando el reenvío de mail al botón de correo (antes solapaban).

**Modelo de datos:** `EstadoEmisionEntidad<TRecibo>(EntidadId, EntidadNombre, Recibo?)` en
Core/Models/Resultados (genérico para reusar entre CP/CM sin acoplar Core a una app). La UI proyecta a
`EmisionMasivaItem` (superset de `ReciboItem` que contempla "sin recibo").

**Bug corregido (ítems del grupo no aparecían en el recibo):** `ReciboRepository.GetPorClaveAsync` no
incluía `.Include(r => r.Lineas)`, así que al **reanudar/reintentar** un recibo existente sus líneas
quedaban vacías y el PDF/mail caía al texto libre, perdiendo el detalle itemizado. Se agregó el Include en
ambas apps. Además, mientras el recibo está `Pendiente` (sin CAE), `EmitirOResumirAsync` re-sincroniza el
snapshot de líneas con los ítems actuales del grupo; con CAE ya emitido el detalle queda congelado.

**Grupos:** el panel de edición se gatea con un flag `EnEdicion` (sólo editable al crear o seleccionar un
grupo); `Guardar` requiere nombre + ≥1 ítem y `Agregar ítem` valida los campos, todo reactivo vía
`CanExecute` (alineado con el resto de la app).

**Por qué:** dos tablas con tres columnas de error para tres pasos consecutivos colapsaban información y
confundían; Recibos ya había resuelto el mismo problema con una columna de estado + acciones por fila, así
que se replicó ese patrón para dar control por agencia/empresa desde la misma pantalla.

**Tests:** `PuertoBB.Tests/ServiceFlowTests.cs` suma cobertura de `EmitirMasivoAsync(enviarMail:false)`,
`GetEstadoMasivoAsync`, `EnviarMasivoAsync` y el mantenimiento de líneas multi-ítem tras un reintento.

**Migraciones consolidadas + seed (pre-producción):** como la plataforma aún no está en producción, se
adopta la política de **una sola migración por release**: se borraron todas las migraciones incrementales
de desarrollo y se regeneró una única `Inicial` por contexto (`Migrations/CamaraPortuaria`,
`Migrations/CentroMaritimo`) con `dotnet ef migrations add Inicial`. El `SeedData` de ambas apps ahora
siembra los grupos **con líneas (ítems)** para poder probar la emisión masiva multi-ítem de inmediato.
Consecuencia operativa: las bases de desarrollo existentes traen el historial de migraciones viejo, así que
hay que **borrar los `.db` de dev** (`%LocalAppData%\PuertoBB\CamaraPortuaria\camara-portuaria.db` y
`...\CentroMaritimo\centro-maritimo.db`) para que la `Inicial` y el seed se apliquen en limpio.

**Docs:** `doc/diseño/emision-masiva.md`; `doc/negocio/funcionalidad-compartida.md` actualizado.

---

## D-20 — Clave de emisión individual: N recibos por período, reintento del Pendiente

**Fecha:** 2026-06-10.

**Decisión:** Se permiten N recibos individuales (sin grupo) por (entidad, período). `FiltrarPorClave(grupoId: null)` retorna exclusivamente el recibo `Pendiente` (sin CAE) si existe; si no hay ninguno, devuelve null y se crea uno nuevo. Recibos ya emitidos/enviados del mismo período no bloquean la nueva emisión.

**Motivo:** el negocio requiere cobros extraordinarios independientes en el mismo período (documentado en `funcionalidad-compartida.md`). La clave anterior bloqueaba el segundo recibo porque igualaba el "ya existe" a cualquier recibo individual, incluyendo los completos.

**Efecto en CM:** también se excluyen los recibos `EsConsolidadoVouchers` del filtro de individuales, para que un mes con cierre cerrado no bloquee la emisión individual a esa agencia.

**Invariante mantenido:** para recibos de grupo (grupoId non-null) el comportamiento no cambia — la unicidad sigue siendo la combinación (entidad, grupoId, período) a través de `EmisionGrupo`.

---

## D-21 — Baja del "apoderado fiscal" (CM) y "Emisor" dentro de la pestaña AFIP/ARCA

**Fecha:** 2026-06-11.

**Decisión:** Se elimina por completo el feature **apoderado fiscal** del Centro Marítimo: pestaña de Configuración, propiedades/commands del `ConfiguracionViewModel`, campos de entidad (`Configuracion.UsarApoderado/NombreApoderado/CuitApoderado` y el snapshot `Recibo.EsApoderado/NombreApoderado/CuitApoderado`), su configuración EF, y el uso en `AfipConfigProvider` (vuelve a usar siempre `Cuit` del emisor), `CentroMaritimoReciboService` (sin snapshot de apoderado) y `CentroMaritimoPdfService` (sin leyenda de apoderado; `BuildEmisor` ya no recibe el `Recibo`). La migración `Inicial` del contexto CM se **regeneró** (convención: una migración por contexto) y se borraron las `.db` de dev. Además, en **ambas apps** la sección **"Datos del emisor"** deja de ser una pestaña propia y pasa a ser la primera subsección de la pestaña **"AFIP / ARCA"** (los bindings/commands del emisor no cambian).

**Motivo:** simplificación pedida por el usuario; el apoderado no se usa en la operación real y dejaba datos/lógica muertos. Agrupar Emisor con AFIP/ARCA junta en una sola pantalla todo lo fiscal del emisor.

---

## D-22 — La grilla de Vouchers ya no muestra columna "Estado"

**Fecha:** 2026-06-11.

**Decisión:** Se elimina la columna "Estado" del `DataGrid` de la página Vouchers (CM) y la propiedad `VoucherItem.EstadoTexto` que la alimentaba. El estado del ciclo de un voucher (Pendiente / Emitido / Completo) se consulta en la página **Cierre de Período**, que lo deriva del recibo consolidado. En Vouchers, la propiedad `Consolidado` sigue gobernando qué vouchers se pueden editar/eliminar.

**Motivo:** el estado por voucher duplicaba el estado del recibo que lo consolidó y generaba confusión (dos lugares mostrando lo mismo con matices distintos).

---

## D-23 — Los datos reales del SeedData se mantienen (cierra P2-10 de la auditoría 2026-06-10)

**Fecha:** 2026-06-11.

**Decisión:** Los CUITs y emails reales de empresas/agencias presentes en `SeedData` de ambas apps **se mantienen tal cual**. El repositorio es privado y el seed solo corre con `ModoDemo=true`; en producción (`ModoDemo=false`) la base nace vacía y esos datos nunca se siembran.

**Motivo:** decisión explícita del usuario (2026-06-11) al cerrar el ítem P2-10 de la auditoría: anonimizar o externalizar no aporta valor en un repo privado de uso interno.

---

## D-24 — Certificado AFIP guardado en la base y baja del cifrado DPAPI

**Fecha:** 2026-06-12.

**Decisión:** El certificado AFIP (`.p12` o `.crt` + `.key`) deja de copiarse a `%LOCALAPPDATA%\...\Certificados\` y de guardarse por **ruta**. Ahora su contenido se persiste en la base, en `PuntoDeVenta` (columnas BLOB `CertificadoContenido` y `CertificadoKeyContenido`); `CertificadoRuta`/`CertificadoKeyRuta` quedan solo como nombre de archivo para mostrar. `Afip.Net` carga el `X509Certificate2` desde bytes en memoria (`X509CertificateLoader.LoadPkcs12` / `X509Certificate2.CreateFromPem`, `EphemeralKeySet`), sin tocar el disco; los campos por ruta de `AfipOptions` se conservan como alternativa.

Además se **revierte el cifrado DPAPI** introducido proactivamente en D-16 (no había sido pedido): se elimina `ISecretProtector`/`DpapiSecretProtector` y todos sus usos (contraseña del certificado y contraseña SMTP, que ahora se guardan en texto plano) y se quita el cifrado DPAPI del cache de ticket WSAA (`FileTicketStore` pasa a JSON plano en disco). Se quita el paquete `System.Security.Cryptography.ProtectedData` de `PuertoBB.Services` y `Afip.Net`. Las migraciones `Inicial` de ambos contextos se regeneraron (convención: una por contexto) y se borró la `.db` de dev.

**Motivo:** pedido explícito del usuario. (1) Guardar el certificado en la base lo incluye en el backup (VACUUM INTO) y evita archivos sueltos/rutas rotas. (2) El usuario no había solicitado ningún cifrado y prefiere no tenerlo.

**Nota de seguridad (acordada con el usuario):** es un downgrade deliberado. El `.db` y el cache de ticket quedan con secretos en **texto plano** (contraseñas SMTP y de certificado, y la clave privada `.key`).

**UI:** en Configuración, el campo del certificado pasó de `StackPanel` horizontal a un `Grid` de dos columnas (`*` + `Auto`): ancho fijo, sin empujar el botón "Examinar…", mostrando solo el nombre del archivo (ruta completa en el ToolTip).

## D-25 — RG 5616 (condición IVA del receptor) + FEParamGet* en diagnóstico + constancia de inscripción

**Fecha:** 2026-06-12.

**Contexto:** la investigación del mismo día (ver `doc/arquitectura/afip-integracion.md`) detectó que la emisión real sería rechazada con error **10242**: la RG 5616 exige `CondicionIVAReceptorId` en `FECAESolicitar` (homologación ya lo valida; excluyente en producción desde el 01/09/2026) y el mapper serializaba `0`. Se aprovechó para sumar dos capacidades estándar de los clientes AFIP de referencia. La **NC parcial se descartó** por decisión de negocio.

**Decisiones:**

1. **Catálogo único** `PuertoBB.Core/Afip/CatalogoCondicionesIvaReceptor.cs` (códigos 1, 4, 5, 6, 7, 8, 9, 10, 13, 15, 16), patrón de `CatalogoComprobantesAfip`. Plano, sin clase fiscal: la semántica de `Cmp_Clase` de AFIP no está bien documentada y filtrar el combo podría bloquear emisiones válidas; la lista autoritativa por clase la muestra el diagnóstico.
2. **Una sola fuente de verdad en el receptor:** `Empresa`/`Agencia` reemplazan el string libre `CondicionIva` por **`CondicionIvaId (int?)`**; el texto se deriva del catálogo al armar el snapshot del recibo (`ReceptorCondicionIva` + nuevo `ReceptorCondicionIvaId`). El PDF sigue leyendo el texto del snapshot.
3. **`required int CondicionIvaReceptorId` en toda la cadena de DTOs** (`ComprobanteAfipRequest` → `AfipComprobanteRequest` → `WsfeCaeRequest` → `FECAEDetRequest`): el compilador impide reintroducir el 0 silencioso. La nulabilidad vive solo en entidades; los ReciboService validan ANTES de llamar a AFIP con mensaje accionable (en masiva, fallo por entidad; recibo queda Pendiente). Anulación: snapshot ?? entidad ?? error. `AfipErrores` traduce 10242/10243/10246.
4. **Diagnóstico extendido:** `IWsfeClient`/`WsfeService` envuelven `FEParamGetPtosVenta`/`TiposCbte`/`CondicionIvaReceptor` (mapeo defensivo: "S"/"N", fechas "NULL", error 602 → lista vacía). `ProbarConexionAsync` valida PV habilitado/no bloqueado/CAE y tipo vigente; cada chequeo en try/catch propio y **nunca degrada `AutenticacionOk`**; `DiagnosticoAfip` suma `PuntoVentaOk`/`TipoComprobanteOk` (null = no verificable, p. ej. lista de PV vacía en homologación) y las condiciones IVA válidas.
5. **Constancia de inscripción** (`ws_sr_constancia_inscripcion`): nuevo `Afip.Net/Padron/` + `PadronSoapClient`/`PadronMapper` (cliente generado con svcutil en `Soap/Generated/PadronReference.cs`). Derivación de condición: monotributo→6, impuesto 30→1, impuesto 32→4, sino→15; `errorConstancia` → resultado parcial con observaciones; fault "no existe persona" → null (detección encapsulada en un solo método). El TA usa el cache por (CUIT, servicio) existente. Adaptador `IAfipPadronService`/`AfipPadronService` único para ambos modos (mock = `MockPadronClient`, patrón D-16). Botón **"Validar en ARCA"** en el ABM de Empresas/Agencias autocompleta razón social/domicilio/condición (no pisa con vacío). Requiere delegar el servicio al certificado.
6. **Seed demo:** `CondicionIvaId = 1` (todas S.A./S.R.L. reales, RI con altísima probabilidad). Migraciones `Inicial` regeneradas (convención de squash) y `.db` de dev borradas.
7. `NoWarn CS8981` en `Afip.Net.csproj`: el contrato generado del padrón trae nombres en minúscula del WSDL de ARCA.

**Resultado:** 159 tests verdes (28 nuevos). La emisión en homologación queda desbloqueada del lado del código.

---

## D-26 — Autenticación de correo flexible: Básica + OAuth2 (ambos flujos)

**Fecha:** 2026-06-13.

**Contexto:** al probar el envío real, Microsoft 365/Outlook rechaza la autenticación básica de SMTP con
`535 5.7.139 Authentication unsuccessful, basic authentication is disabled` — Microsoft **retiró la auth
básica** para SMTP. No es un bug: el código hacía `AuthenticateAsync(usuario, password)` correctamente.

**Decisión:** el correo deja de ser solo "usuario + contraseña". Se agrega un **selector de autenticación**
en Configuración → Correo con **`Ninguna` / `Básica` / `OAuth2`**. OAuth2 (SASL **XOAUTH2**) admite
**proveedor** (`Microsoft 365` empresa / `Google` / `Personalizado` / `Outlook.com personal`) y **ambos flujos
seleccionables por caso** (pedido explícito del usuario: "que funcione para cualquier tipo de autenticación y
plataforma"):
- **Interactivo:** authorization code + **PKCE** con redirect a un loopback local (`http://localhost:{puerto}`);
  el usuario consiente una vez en el navegador y se guarda un **refresh token**. Cubre Microsoft 365, personal
  `@outlook.com`/`@hotmail.com` y Gmail.
- **Cliente:** `client_credentials` (Tenant + Client ID + Secret), sin navegador. Para casillas Microsoft 365
  de empresa administradas (requiere `SMTP.Send` como app permission + `Set-CASMailbox`).

La **básica** sigue cubriendo Gmail (contraseña de aplicación), Brevo/SendGrid/SES (API key como contraseña),
Yahoo, Zoho y SMTP propios — sin OAuth.

**Cómo se implementa:**
- **Core** (`PuertoBB.Core/Models/Mail/`): enums `MailAutenticacion`/`OAuthProveedor`/`OAuthFlujo`;
  `MailConfig` ampliado con los campos OAuth + `Validar()` (validación por modo); `OAuthPresets`
  (única fuente de endpoints/scope/host por proveedor; tenant `common` por defecto en Microsoft);
  `MailErrores.Describir` (traduce el 5.7.139 a un mensaje accionable que sugiere OAuth2).
- **Services** (`PuertoBB.Services/Mail/Oauth/`): `OAuthTokenProvider` (Singleton, cachea access tokens en
  memoria; flujos `refresh_token` y `client_credentials`) y `OAuthInteractiveFlow` (`HttpListener` + PKCE +
  `Process.Start` al navegador, decodifica el email del `id_token`). `MailService` autentica vía
  `SaslMechanismOAuth2` en un helper `AutenticarAsync` reusado por `EnviarReciboAsync` y `ProbarConexionAsync`.
  Sin paquetes nuevos: XOAUTH2 ya viene en MailKit 4.17 y el token se pide con `HttpClient`. Registro en
  `AddPuertoBBMail`.
- **Entidades:** columnas nuevas en `Configuracion` de ambas apps; mapeo en ambos `MailConfigProvider`.
- **UI:** pestaña Correo de **ambas apps** con el selector, secciones Básica/OAuth2, combo de proveedor (sugiere
  host/puerto), radios de flujo, botón **"Iniciar sesión…"** (`IOAuthInteractiveFlow`) que guarda
  refresh token + email, y `PasswordBox` para el client secret (mismo patrón que `SmtpPasswordBox`).
- **Secretos en texto plano** (refresh token / client secret / password), coherente con **D-24**.
- **Migraciones:** se regeneró la `Inicial` de cada contexto y, de paso, se **reorganizó la estructura** para que
  cada contexto tenga **su propio directorio** según la convención de **D-19**: `Migrations/CamaraPortuaria/` y
  `Migrations/CentroMaritimo/` (se eliminaron la migración suelta en la raíz `Migrations/` y la carpeta drift
  `CentroMaritimoDb/`). Los tests usan `EnsureCreated()`, así que toman el esquema nuevo sin tocar migraciones.

**Por qué XOAUTH2 + presets y no MSAL:** un POST genérico a los token endpoints (configurable por proveedor)
cubre Microsoft y Google sin atar el proyecto a una SDK específica, manteniendo la opción `Personalizado`.

**Tests:** unit tests puros (sin red) de `OAuthPresets`, `MailConfig.Validar` por modo y `MailErrores`.
Total **197 verdes**. El flujo interactivo (navegador) y el envío real se validan manualmente.

**Docs:** `doc/usuario/correo-oauth.md` (alta de app en Azure/Google, scopes, redirect loopback,
`Set-CASMailbox`); `doc/usuario/paso-a-produccion.md` (paso 4 actualizado); `doc/arquitectura/datos.md`
(campos de `Configuracion`).

**Ajustes durante la prueba real (2026-06-13):**
- **Logging del login OAuth:** `OAuthInteractiveFlow` y `OAuthTokenProvider` no tenían `ILogger` y las fallas
  solo aparecían como toast efímero. Ahora loguean cada falla a `LogWarning` (incl. el `error`/`error_description`
  que el proveedor devuelve al loopback) → quedan en `%LocalAppData%\PuertoBB\<App>\Logs\app-*.log`. Limitación
  inherente: si el navegador rechaza `redirect_uri`/`scope`, el proveedor **no** redirige al loopback y la app lo
  ve como timeout (el error real queda en el navegador).
- **Cuentas personales Outlook.com:** rechazan el recurso `outlook.office365.com`. El scope de Microsoft
  (interactivo) pasó a **`https://outlook.office.com/SMTP.Send`** (universal personal+empresa) y se agregó el
  proveedor **`OutlookPersonal`** (enum=3) con host **`smtp-mail.outlook.com`**. El cliente (app-only) mantiene
  `outlook.office365.com/.default` (solo empresa).

**Pendiente real (no testeable sin credenciales):** prueba del usuario registrando la app en Azure (flujos
Interactivo y Cliente) y/o Google Cloud, y un envío real desde la casilla destino.

---

## D-27 — Multi-cuenta de correo (una activa) + autocompletado por proveedor

**Fecha:** 2026-06-13.

**Decisión:** el correo deja de ser una config plana en `Configuracion` y pasa a ser una **lista de cuentas**
con **una activa**, espejo de los **Puntos de venta** (D-17). Nueva entidad `CuentaCorreo` (una por app) con
todos los campos SMTP/auth/OAuth + `Nombre` + `Activo`; `Configuracion` pierde los campos planos de correo y gana
`List<CuentaCorreo> CuentasCorreo` + `[NotMapped] CuentaCorreoActiva`. El `MailConfigProvider` arma el `MailConfig`
desde la **cuenta activa** (si no hay, queda sin configurar). Además, en el form de cada cuenta hay un combo
**Proveedor** (Microsoft 365 / Outlook.com personal / Google / Otro) que **autocompleta** host/puerto/seguridad y
fija el modo OAuth2 (reusa `OAuthProveedor`/`OAuthPresets`, sin catálogo nuevo), y los campos OAuth son
**condicionales** según proveedor/flujo (con Google no se muestra Tenant; "Cliente" solo para Microsoft empresa; etc.).

**Cómo se implementa:**
- Entidad `CuentaCorreo` + `CuentaCorreoConfiguration` (FK a Configuracion, cascade, seed "Principal" activa) por app.
- `IConfiguracionRepository` + impl: `GetCuentasCorreoAsync`/`Guardar`/`Eliminar`/`MarcarCuentaCorreoActivaAsync`
  (espejo de los de PuntoDeVenta); `GetAsync` incluye `CuentasCorreo`.
- UI (ambas apps): pestaña Correo rediseñada como **master-detail** (grilla de cuentas + form), con
  `CuentaCorreoItem`, props `Cta*` y comandos Nuevo/Editar/Guardar/Cancelar/Eliminar/MarcarActiva/Probar/IniciarSesión.
  "Probar conexión" prueba la **cuenta activa**.
- Migraciones `Inicial` regeneradas por contexto (agrega `CuentasCorreo`, quita columnas de correo de `Configuracion`).

**Por qué:** pedido del usuario — varias casillas (ej. Ventas/Administración) con una por defecto, y menos
configuración a mano (elegir proveedor y listo). Espejar PuntoDeVenta mantiene la consistencia del patrón.

**Tests:** repo de `CuentaCorreo` (seed activa, marcar activa única, eliminar, cuenta activa reflejada en
`Configuracion`). Total **201 tests verdes**.

**Docs:** `doc/arquitectura/datos.md` (entidad `CuentaCorreo`), `doc/usuario/correo-oauth.md`.

---

## D-28 — Estados del recibo normalizados a un eje fiscal + ejes derivados (incobrable a nivel recibo)

**Fecha:** 2026-06-13.

**Decisión:** el enum lineal `ReciboEstado` (`Pendiente/Emitido/Enviado/Pagado/Anulado`) mezclaba **tres ejes
ortogonales** (fiscal, envío de mail, cobro) en una sola variable, por lo que marcar Pagado pisaba "Enviado" y
anular pisaba "Pagado". Se reemplaza por **un único estado de flujo persistido** `EstadoFiscal`
(`Pendiente/Emitido/Anulado`); el **envío** y el **cobro** se **derivan** de columnas que ya existían
(`FechaEnvioMail`/`UltimoErrorMail` y `FechaPago`). Toda la presentación y las acciones se centralizan en
`EstadoReciboHelper` + `AccionesRecibo` (única fuente), consumidos vía la interfaz `IReciboEstadoView` que
implementan ambas entidades `Recibo`. La grilla pasa a **dos columnas limpias**: `Estado` (fiscal + cobro) y
`Envío` (solo mail).

Además, el viejo flag **`Empresa/Agencia.EsMoroso`** (reputación a nivel cliente, "solo informativo") se
**elimina** y se reemplaza por un concepto a nivel recibo: **Incobrable** (baja de la deuda), modelado como
hermano de `Pagado` en el eje de cobro con dos campos nuevos `FechaIncobrable` + `MotivoIncobrable`
(excluyente con `FechaPago`). En Control de Pagos el botón "Moroso" pasa a "Marcar incobrable" / "Quitar
incobrable" (vía el service) y el checkbox "Incluir morosos" a "Incluir incobrables".

**Cómo se implementa:**
- `Core`: enum `EstadoFiscal` (reemplaza `ReciboEstado`) + enums derivados `EstadoEnvio`/`EstadoCobro`;
  interfaz `IReciboEstadoView`; `EstadoReciboHelper` ampliado (`EtiquetaEstado`/`EtiquetaEnvio`/`Cobro`/
  `EstaVencido`/`EsCompleto`) + `AccionesRecibo.De`.
- `Infrastructure`: `Recibo.Estado→EstadoFiscal` + `FechaIncobrable`/`MotivoIncobrable`; baja de `EsMoroso`
  (entidades, configs y `SetMorosoAsync`); queries de repos (Pendiente/Anulado y vencidos excluyen pagados e
  incobrables); `FiltroPendientes.ExcluirMorosos→ExcluirIncobrables`. Migración `Inicial` regenerada por
  contexto (lossless: Enviado/Pagado viejos → Emitido, recuperados de las fechas).
- `Services`: `MarcarPagado` setea `FechaPago`; nuevos `MarcarIncobrableAsync`/`QuitarIncobrableAsync`; el
  envío deja de setear "Enviado" (solo `FechaEnvioMail`); `VoucherService.MapEstado` derivado del modelo nuevo.
- `UI` (ambas): `ReciboItem`/`EmisionMasivaItem` desde el helper; dos columnas; Control de Pagos con incobrable;
  converter de color `Moroso→Incobrable` y "Enviado" alineado a `#E0F7FA` (los docs decían `#FFF9C4`).

**Por qué:** pedido del usuario de unificar y hacer consistentes los estados entre secciones. Mantener un único
eje persistido (la pregunta legal "¿tiene CAE?") con el resto derivado evita dos fuentes de verdad que divergen,
y permite combinaciones antes imposibles (p. ej. "Pagado" + "Enviado"). Es la evolución natural de **D-05**
("Vencido" derivado), extendida a los ejes de envío y cobro.

**Tests:** `EstadoReciboHelperTests` reescritos (dos columnas, combinación Enviado+Pagado, incobrable) +
casos de `MarcarIncobrable`/`QuitarIncobrable` en `ServiceFlowTests`. Total **215 tests verdes**.

**Docs:** `doc/arquitectura/datos.md`, `doc/arquitectura/flujos.md`, `doc/negocio/funcionalidad-compartida.md`,
`doc/negocio/cierre-periodo.md`, `doc/diseño/paletas-color.md`, `doc/diseño/fluent-navigation.md`.

## D-29 — Quick wins de UX en Configuración → Correo

**Fecha:** 2026-06-17.

**Decisión:** revisión de arquitecto sobre el flujo de configuración de correo (D-26/D-27). El backend estaba
sólido pero la capa de UX tenía trampas que hacían que el usuario creyera haber configurado bien y después no
pudiera enviar. Se aplican **quick wins de alto impacto** sin rediseñar el layout ni hacer un wizard, y se
mantiene la jerga (Client ID, Tenant, Flujo) **acompañada** con texto explicativo (usuario administrativo con
seguimiento técnico).

**Cómo se implementa:**
- **Validar al guardar:** `GuardarCuentaAsync` ahora llama `ConstruirMailConfig().Validar()` (validador por
  modo que ya existía pero no se usaba). Bloquea guardar una cuenta incompleta con mensaje accionable; pasa a
  exigir email remitente.
- **Probar la cuenta en edición:** sobrecarga `IMailService.ProbarConexionAsync(MailConfig)` (refactor de
  `MailService`, implementada también en `FakeMailService`) + botón **"Probar esta cuenta"** en el formulario.
  Permite probar (incluso tras "Iniciar sesión…") antes de guardar/activar. El "Probar conexión" de la lista
  sigue probando la cuenta activa.
- **No perder el login OAuth:** flag `_loginSinGuardar`; al Cancelar con un login no guardado se pide
  confirmación (`IDialogService.ShowConfirmAsync`).
- **Confirmar al eliminar:** `EliminarCuentaAsync` pide confirmación modal (antes borraba directo, violando la
  regla de UX); avisa si la cuenta es la activa.
- **Destrabar Gmail:** elegir "Google / Gmail" deja la cuenta en **Básica** (contraseña de aplicación, el
  camino recomendado) en vez de forzar OAuth2; OAuth2 sigue elegible a mano. Guía in-app y `correo-oauth.md`
  actualizadas.
- **Andamiaje de jerga (XAML):** radios de Flujo relabelados ("Interactivo — iniciás sesión en el navegador" /
  "Cliente — secreto de aplicación, sin navegador") y hints bajo Client ID / Tenant ID / Client Secret
  ("Azure → …").

**Por qué:** percepción del usuario de que "algunas cosas no estaban del todo claras". Las trampas eran
concretas: validación existente sin usar, prueba sobre la cuenta equivocada, login que se perdía, borrado sin
confirmar y Gmail empujado al camino que la propia ayuda desaconseja.

**Alcance:** ambas apps (`CamaraPortuaria.UI` y `CentroMaritimo.UI`) + servicios compartidos (`IMailService`,
`MailService`, `FakeMailService`).

**Fuera de alcance (anotado):** wizard por proveedor, banner de salud de la cuenta activa al tope del tab,
columna de estado/health en la grilla, reordenar campos.

**Docs:** `doc/usuario/correo-oauth.md`.

---

## D-30 — Acciones masivas de emisión IDEMPOTENTES (no fallar por "ya hay emitidos")

**Decisión:** En Emisión masiva, las acciones masivas (`EmitirMasivoAsync`, "Emitir" / "Emitir y
enviar") son idempotentes: emiten/envían lo pendiente y **omiten** los recibos ya completos
(emitido + enviado) sin contarlos como error. Un recibo ya completo devuelve
`ResultadoEmisionPorEntidad.Omitida` (`Exito=false`, **`Omitido=true`**) — nunca `Fallo("Ya existe…")`.
El resumen (`EjecutarMasivoAsync`) cuenta los omitidos aparte y reporta **éxito** si no hubo errores
reales. Si al "Emitir y enviar" hay recibos ya enviados, el VM pregunta "¿Reenviarlos también?"; al
aceptar, `EmitirMasivoAsync(reenviarYaEnviados: true)` fuerza el reenvío vía
`ProcesarReciboAsync(forzarEnvio: true)`. Idéntico en **ambas apps**.

**Motivo:** El comportamiento previo devolvía `Fallo("Ya existe un recibo para este período.")` para
cada recibo completo, así que "Emitir y enviar" sobre un grupo con algunos ya emitidos reportaba
"Emisión fallida" y no procesaba nada — cuando lo correcto es hacer lo que falte y seguir. Es el
mismo patrón que ya tenía el flujo de **Cierre de período** de Centro Marítimo
(`ProcesarCierreAgenciaAsync` → `ResultadoCierrePorAgencia.Omitida`, `MostrarResultadoMasivo`,
`ReenviarMailsAsync` con confirmación); acá se lleva a la Emisión masiva de las dos apps.

**Regresión a evitar:** NO re-introducir `Fallo("Ya existe…")` en `EmitirOResumirAsync`. La regla
está documentada en `doc/diseño/emision-masiva.md` y cubierta por
`ServiceFlowTests.EmitirMasivo_SegundaVez_OmiteCompletos_SinFallarNiDuplicar` y
`EmitirMasivo_ReenviarYaEnviados_ReenviaLosCompletos`.

**Alcance:** `PuertoBB.Core` (`ResultadoEmisionPorEntidad.Omitida`/`Omitido`, interfaces
`I*ReciboService.EmitirMasivoAsync`), `PuertoBB.Services` (`*ReciboService`), ambas
`EmisionMasivaViewModel`.

**Docs:** `doc/diseño/emision-masiva.md`.

---

## D-31 — Recuperar recibos Pendiente (emisión fallida, sin CAE) + monto esperado pre-emisión

**Decisión:** Cuando una emisión falla por validación (p. ej. AFIP rechaza `ImporteTotal <= 0`) el recibo
queda Pendiente (sin CAE). Se agregan tres vías de recuperación, idénticas en ambas apps:

1. **Corregir el grupo y re-emitir.** El "Emitir" POR FILA de Emisión masiva pasa a usar siempre
   `EmitirDeGrupoAsync` (antes usaba `ReintentarAsync` para un recibo existente). `EmitirDeGrupoAsync`
   → `EmitirOResumirAsync` re-sincroniza importe/líneas del grupo ACTUAL antes de reintentar el CAE, así
   un Pendiente trabado por datos viejos (monto cero ya corregido en el grupo) se recupera. `ReintentarAsync`
   NO re-sincroniza el importe — por eso fallaba.
2. **Editar el recibo Pendiente** (`EditarReciboPendienteAsync` + `EditarReciboDialog`, botón en Recibos):
   edita líneas/importe; rechaza si ya tiene CAE.
3. **Eliminar el recibo Pendiente** (`EliminarReciboPendienteAsync` + `IReciboRepository.EliminarPendienteAsync`,
   botón en Emisión masiva fila y Recibos toolbar): borra recibo + líneas + vínculo EmisionGrupo (y libera
   los vouchers en CM); rechaza si ya tiene CAE.

Además, la columna **Importe** de Emisión masiva muestra el **monto esperado del grupo**
(`EstadoEmisionEntidad.ImporteEsperado`) para los miembros aún no emitidos, para poder validar el importe
ANTES de emitir (antes mostraba "—").

**Motivo:** Un recibo con monto cero hacía fallar la emisión y quedaba trabado; corregir el grupo y
reintentar por fila no lo recuperaba (el reintento conservaba el importe viejo). Faltaba además poder ver el
monto antes de emitir para detectar el cero. Editar/eliminar dan salida directa para cualquier Pendiente.

**Gates:** `EmisionMasivaItem.EsEliminable` = `ReciboId != null && !CaeOk`; `ReciboItem.EsEditable` =
`EstadoFiscal == Pendiente`.

**Alcance:** `PuertoBB.Core` (`EstadoEmisionEntidad.ImporteEsperado`, interfaces de servicio y de repo,
`IDialogService.ShowEditarReciboAsync`), `PuertoBB.Services`/`PuertoBB.Infrastructure`, ambas apps UI
(VMs Recibos/EmisionMasiva, items, páginas, `DialogService`, nuevo `EditarReciboDialog`).

**Tests:** `GetEstadoMasivo_DevuelveImporteEsperadoDelGrupo`,
`EmitirDeGrupo_TrasFalloCae_ReSincronizaImporteDelGrupoCorregido`,
`EditarReciboPendiente_ActualizaLineasEImporte_YRechazaConCae`,
`EliminarReciboPendiente_BorraSinCae_YRechazaConCae`.

**Docs:** `doc/diseño/emision-masiva.md`.

---

## D-32 — Recibo consolidado complementario (vouchers olvidados tras emitir, CM)

**Decisión:** Permitir **más de un consolidado de vouchers por `(Agencia, Período)`** cuando los anteriores
**ya tienen CAE**. Si después de emitir aparece un voucher olvidado (se carga libre, `ReciboId IS NULL`),
volver a **Emitir/Cerrar** genera un **recibo consolidado complementario** —comprobante adicional con su
propio número y CAE, solo por los vouchers libres— y **deja intacto el original** (sin Nota de Crédito).

**Cambio de invariante:** de *"un consolidado no anulado por período"* a **"un solo consolidado *Pendiente*
(sin CAE) por período; varios con CAE"**. Se materializa cambiando el filtro del índice único parcial de
`Recibo` de `EsConsolidadoVouchers=1 AND EstadoFiscal<>'Anulado'` a
`EsConsolidadoVouchers=1 AND EstadoFiscal='Pendiente'`.

**Motivo:** Antes, agregar un voucher olvidado a un período ya emitido obligaba a **anular** el consolidado
(emite NC, libera todos los vouchers) y **reemitir todo** → 3 documentos a la agencia por un solo voucher.
El complementario lo resuelve con un comprobante adicional. La anulación+reemisión sigue disponible para
**corregir/eliminar** vouchers ya consolidados.

**Cómo cae solo:** `GetPendientesByPeriodoAsync` ya excluye vouchers consolidados, así que la rama de
"nuevo consolidado" de `ProcesarCierreAgenciaAsync` sirve para la 1ª emisión y para el complementario sin
ramas nuevas. Se renombró `IReciboRepository.GetConsolidadoAsync` → **`GetConsolidadoPendienteAsync`**
(filtro `EstadoFiscal==Pendiente`; el target de reintento) y se quitó el corte `EsCompleto → Omitida`.

**Estado en UI:** una agencia es **Pendiente** si tiene vouchers libres (1ª emisión o complementario);
`AgenciaCierrePeriodoVm` ahora expone la lista `Consolidados` (0..n) y cada `VoucherCierreVm` su comprobante
(o "Libre"). La página de Cierre tiene acciones de PDF/mail **por recibo** en el detalle y, por fila, emisión
de los vouchers libres (la confirmación avisa cuando será complementario).

**Migración:** se editó la `Inicial` de `CentroMaritimoDbContext` (filtro del índice) — convención de una
migración por contexto (D-24/D-19); se respaldó la `.db` de dev para que `MigrateAsync` la recree.

**Tests:** `EmitirRecibos_VoucherOlvidadoTrasConsolidadoConCae_CreaComplementario`,
`GetCierrePeriodo_ConsolidadoEmitidoMasVoucherLibre_QuedaPendiente`.

**Alcance:** `PuertoBB.Core` (`AgenciaCierrePeriodoVm`/`ConsolidadoCierreVm`/`VoucherCierreVm`,
`IReciboRepository`), `PuertoBB.Services` (`CentroMaritimoReciboService`, `VoucherService`),
`PuertoBB.Infrastructure` (`ReciboRepository`, `ReciboConfiguration`, migración `Inicial` CM),
`CentroMaritimo.UI` (`CierrePeriodoViewModel` + `CierrePeriodoPage.xaml`).

**Docs:** `doc/negocio/cierre-periodo.md`, `doc/usuario/manual-centro-maritimo.md`, `doc/arquitectura/flujos.md`.
