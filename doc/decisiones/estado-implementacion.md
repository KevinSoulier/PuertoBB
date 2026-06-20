# Estado de implementación — PuertoBB

> Resultado de la sesión de implementación end-to-end (2026-06-05).
> Punto de partida: documentación completa + esqueleto de código (stubs vacíos).
> Resultado: plataforma funcional de punta a punta en modo demo, con tests verdes.

> **Actualización 2026-06-06 (D-16):** el cliente AFIP se extrajo a una **librería neutra y reutilizable
> `Afip.Net`** (multi-servicio, lista para sumar Remito Electrónico a futuro). `PuertoBB.Services/Afip`
> quedó como adaptador (`IAfipService` → `Afip.Net`) + `FakeAfipService`. Se sumaron: cache del Ticket de
> Acceso por servicio y persistido cifrado (DPAPI), contraseña del certificado cifrada en reposo,
> importación del `.p12`, botón **Probar conexión** y aviso de ambiente. Tests: 29 verdes. Docs:
> `Afip.Net/README.md`, `doc/usuario/afip-configuracion.md`.

> **Actualización 2026-06-12 (D-25):** cumplimiento de la **RG 5616** — condición frente al IVA del
> receptor obligatoria en la emisión (catálogo en Core, `CondicionIvaId` en Empresa/Agencia + snapshot
> en Recibo, `required` en toda la cadena de DTOs, validación pre-AFIP con mensaje accionable);
> **`FEParamGet*`** en el diagnóstico de "Probar conexión" (PV habilitado, tipo vigente, condiciones
> IVA válidas); y **constancia de inscripción** (`ws_sr_constancia_inscripcion`) con botón
> **"Validar en ARCA"** en el ABM de empresas/agencias. Tests: **159 verdes**. Las apps corren con
> `Afip = "Real"` apuntando a homologación.

> **Actualización 2026-06-13 (D-26):** **autenticación de correo flexible** — selector
> `Ninguna`/`Básica`/`OAuth2` en Configuración → Correo. OAuth2 (XOAUTH2 vía `SaslMechanismOAuth2`) con
> proveedor Microsoft/Google/Personalizado y **ambos flujos** (Interactivo con PKCE+loopback / Cliente con
> client-credentials). Resuelve el `535 5.7.139` de Microsoft 365/Outlook. `MailService` ahora autentica por
> OAuth2; nuevas piezas `OAuthPresets`/`OAuthTokenProvider`/`OAuthInteractiveFlow`/`MailErrores`. Migraciones
> reorganizadas a una carpeta por contexto (`Migrations/CamaraPortuaria` y `Migrations/CentroMaritimo`).
> Tests: **195 verdes**. Docs: `doc/usuario/correo-oauth.md`.

---

## Qué quedó implementado

### `PuertoBB.Core` — dominio (completo)
- Entidades CP (`Empresa`, `EmailEmpresa`, `GrupoFacturacion`, `EmpresaGrupo`, `Recibo`, `NotaDeCredito`, `Configuracion`) y CM (`Agencia`, `EmailAgencia`, `GrupoFacturacion`, `AgenciaGrupo`, `Barco`, `ContadorVoucher`, `Voucher`, `Recibo`, `NotaDeCredito`, `Configuracion`).
- Enums (`ReciboEstado` sin `Vencido`, `TipoComprobante`), `ServiceResult<T>`, excepciones.
- `EstadoReciboHelper` (cálculo de "Vencido"/atraso en presentación).
- Modelos: `ComprobanteAfipRequest`, `CaeResult`, `AfipConfig`, `MailConfig`, DTOs de resultado (`ResultadoEmisionPorEntidad`, `ResultadoCierrePorAgencia`, `FiltroPendientes`).
- Interfaces de repositorio (por app) y de servicio (`IAfipService`, `IMailService`, PDF dividido por app, servicios de recibo por app, `IVoucherService`, `IDialogService`, providers de config AFIP/Mail).

### `PuertoBB.Infrastructure` — datos (completo)
- `CamaraPortuariaDbContext` / `CentroMaritimoDbContext` con `DbSet`s y aplicación de configuraciones filtrada por namespace.
- Configuraciones EF (Fluent API): índices únicos, **índice único parcial** para recibos consolidados (`WHERE EsConsolidadoVouchers = 1`), `decimal` como `TEXT` en SQLite, enums como `string`, seed de singletons (`Configuracion` Id=1, `ContadorVoucher` Id=1).
- `RepositoryBase<T>` (CRUD, traduce `DbUpdateException` → `ReciboException`) + repositorios concretos por app.
- Design-time factories y **migraciones iniciales** generadas y validadas (`InicialCamara`, `InicialCentro`).
- Extensión DI `AddCamaraPortuariaInfrastructure` / `AddCentroMaritimoInfrastructure`.

### `PuertoBB.Services` — servicios (completo)
- **PDF (QuestPDF)**: `CamaraPortuariaPdfService`, `CentroMaritimoPdfService` (recibo, voucher, **consolidado recibo+vouchers**, nota de crédito).
- **Mail (MailKit)**: `MailService` + `FakeMailService`.
- **AFIP**: orquestación WSAA (`TraBuilder` TRA+CMS PKCS#7, `WsaaTokenCache`) + WSFE contra abstracciones `IWsaaClient`/`IWsfeClient`; `AfipService` real + `FakeAfipService` (CAE simulado).
- **Negocio**: `CamaraPortuariaReciboService` (emisión masiva/individual, anulación con NC, reenvío, pago, dashboard), `CentroMaritimoReciboService` (idem + **cierre de período/consolidación**), `VoucherService`.
- Extensiones DI conmutables (`AddPuertoBBAfip(usarFake)`, `AddPuertoBBMail(usarFake)`).

### `CamaraPortuaria.UI` y `CentroMaritimo.UI` — WPF Fluent (completo)
- Shell Fluent: TitleBar 44px, sidebar 220px con `TreeView` + `Frame`, navegación por DI (`INavigationService`).
- Diálogos modales Fluent por overlay (`IDialogService`: confirm/alert/input) — sin `MessageBox`.
- `App.xaml.cs`: host genérico + DI, logging a archivo con `FileLoggerProvider` propio (archivo diario en `%LocalAppData%\PuertoBB\<App>\Logs`, retención 30, escribe Warning+ y toda excepción), handlers globales de excepción, migración automática y seed de demo, restauración de tema.
- Páginas CP: Inicio (control de pagos), Recibos (+emisión individual, anular, reenviar, pagar), Emisión masiva, Empresas (ABM+emails), Grupos (ABM+miembros), Configuración (+selector de tema claro/oscuro/sistema).
- Páginas CM: Inicio, Vouchers (ABM), Cierre de período, Recibos, Emisión masiva, Agencias, Barcos, Grupos, Configuración.
- `SeedData` por app: empresas/agencias/grupos/barcos/vouchers de ejemplo.

### `PuertoBB.Tests` — xUnit (18 tests, verdes)
- Helpers (`EstadoReciboHelper`, `PeriodoHelper`), `FakeAfipService`, repositorios (índices únicos, contador, `ExisteConsolidado`), y **flujos completos** (emisión masiva + duplicados + fallo de mail, nota de crédito, cierre de período consolidado, alta de voucher) sobre SQLite in-memory.

---

## Cómo correr

**Apps (modo demo, sin AFIP/SMTP reales):**
```
dotnet run --project CamaraPortuaria.UI/CamaraPortuaria.UI.csproj
dotnet run --project CentroMaritimo.UI/CentroMaritimo.UI.csproj
```
Crean su base SQLite en `%LocalAppData%\PuertoBB\<App>` y la siembran con datos de ejemplo.

**Tests:** `dotnet test PuertoBB.Tests/PuertoBB.Tests.csproj`
**Validación integral:** skill `/validar-plataforma`.

Los servicios mock se controlan con dos flags bool en `appsettings.json` (`PuertoBB:MailMockService` y
`PuertoBB:AfipMockService`, ambos default `false` = real). El **modo demo** (`App.ModoDemo`, computado)
es `true` si cualquiera de los dos está activo, y agrega el seed + rótulo "MODO DEMO". Dejar ambos en
`false` para producción.

---

## AFIP real — ✅ implementado (actualización)

El **cliente SOAP real de AFIP** ya está implementado: WSAA/WSFE generados con `dotnet-svcutil` en `PuertoBB.Services/Afip/Soap/`, con `WsaaSoapClient`/`WsfeSoapClient` + `WsfeMapper` (mapeo exento C sin IVA, `CbtesAsoc` en NC) y tests de mapeo/firma. Se activan con `AfipMockService = false` (default). Detalle en `doc/arquitectura/afip-integracion.md`. Tests totales: **23 verdes**.

## Pendiente para producción (fuera del alcance autónomo)

1. **Prueba end-to-end de AFIP con certificado real**: cargar `.p12`, contraseña, CUIT emisor y punto de venta "Exento IVA - WS" desde Configuración, y validar una emisión real contra homologación. Mapeo y firma ya testeados; falta la corrida con credenciales.
2. **SMTP real**: cargar servidor/credenciales desde Configuración para envío real de mail.
3. **Edición fina de emails en ABM**: hoy la edición sincroniza la colección completa al guardar. Funciona; podría refinarse con UI dedicada por email.

> **Backup manual de la base**: ✅ implementado — botón "Generar backup…" en Configuración (VACUUM INTO a una ruta elegida con SaveFileDialog), `IBackupService` en Core + `BackupService` por app, con test del VACUUM INTO. Total tests: **24 verdes**.

Ver decisiones detalladas en [`registro-decisiones.md`](registro-decisiones.md).
