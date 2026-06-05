# Estado de implementación — PuertoBB

> Resultado de la sesión de implementación end-to-end (2026-06-05).
> Punto de partida: documentación completa + esqueleto de código (stubs vacíos).
> Resultado: plataforma funcional de punta a punta en modo demo, con tests verdes.

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
- **Negocio**: `CamaraPortuariaReciboService` (emisión masiva/individual, anulación con NC, reenvío, pago, dashboard), `CentroMaritimoReciboService` (idem + **cierre de período/consolidación** + apoderado fiscal), `VoucherService`.
- Extensiones DI conmutables (`AddPuertoBBAfip(usarFake)`, `AddPuertoBBMail(usarFake)`).

### `CamaraPortuaria.UI` y `CentroMaritimo.UI` — WPF Fluent (completo)
- Shell Fluent: TitleBar 44px, sidebar 220px con `TreeView` + `Frame`, navegación por DI (`INavigationService`).
- Diálogos modales Fluent por overlay (`IDialogService`: confirm/alert/input) — sin `MessageBox`.
- `App.xaml.cs`: host genérico + DI, Serilog (archivo diario en `%LocalAppData%\PuertoBB\<App>\Logs`), handlers globales de excepción, migración automática y seed de demo, restauración de tema.
- Páginas CP: Inicio (control de pagos), Recibos (+emisión individual, anular, reenviar, pagar), Emisión masiva, Empresas (ABM+emails), Grupos (ABM+miembros), Configuración (+selector de tema claro/oscuro/sistema).
- Páginas CM: Inicio, Vouchers (ABM), Cierre de período, Recibos, Emisión masiva, Agencias, Barcos, Grupos, Configuración (+apoderado).
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

El **modo demo** se controla con la constante `App.ModoDemo` en cada `App.xaml.cs`
(`true` = `FakeAfipService` + `FakeMailService` + seed). Ponerlo en `false` para producción.

---

## AFIP real — ✅ implementado (actualización)

El **cliente SOAP real de AFIP** ya está implementado: WSAA/WSFE generados con `dotnet-svcutil` en `PuertoBB.Services/Afip/Soap/`, con `WsaaSoapClient`/`WsfeSoapClient` + `WsfeMapper` (mapeo exento C sin IVA, `CbtesAsoc` en NC) y tests de mapeo/firma. Se activan con `App.ModoDemo = false`. Detalle en `doc/arquitectura/afip-integracion.md`. Tests totales: **23 verdes**.

## Pendiente para producción (fuera del alcance autónomo)

1. **Prueba end-to-end de AFIP con certificado real**: cargar `.p12`, contraseña, CUIT emisor y punto de venta "Exento IVA - WS" desde Configuración, y validar una emisión real contra homologación. Mapeo y firma ya testeados; falta la corrida con credenciales.
2. **SMTP real**: cargar servidor/credenciales desde Configuración para envío real de mail.
3. **Edición fina de emails en ABM**: hoy la edición sincroniza la colección completa al guardar. Funciona; podría refinarse con UI dedicada por email.

> **Backup manual de la base**: ✅ implementado — botón "Generar backup…" en Configuración (VACUUM INTO a una ruta elegida con SaveFileDialog), `IBackupService` en Core + `BackupService` por app, con test del VACUUM INTO. Total tests: **24 verdes**.

Ver decisiones detalladas en [`registro-decisiones.md`](registro-decisiones.md).
