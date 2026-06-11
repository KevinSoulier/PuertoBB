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
