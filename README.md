# PuertoBB

Sistema de gestión de recibos para dos entidades portuarias de Bahía Blanca.

## Entidades

- **Cámara Portuaria** — emisión de recibos a empresas con cuota social y cobros extraordinarios
- **Centro Marítimo** — emisión de recibos a agencias con vouchers por ingreso de buques

## Proyectos

| Proyecto | Tipo | Descripción |
| --- | --- | --- |
| `PuertoBB.Core` | Class Library | Entidades, interfaces, enums, excepciones, modelos |
| `PuertoBB.Services` | Class Library | AFIP/ARCA, generación de PDF, envío de mail, servicios de negocio |
| `PuertoBB.Infrastructure` | Class Library | EF Core, SQLite, repositorios, migraciones |
| `CamaraPortuaria.UI` | WPF App | Interfaz de la Cámara Portuaria |
| `CentroMaritimo.UI` | WPF App | Interfaz del Centro Marítimo |
| `PuertoBB.Tests` | xUnit | Tests de unidad e integración (SQLite in-memory) |

## Cómo correr

```powershell
# Apps (modo demo: AFIP/SMTP simulados + datos de ejemplo sembrados)
dotnet run --project CamaraPortuaria.UI/CamaraPortuaria.UI.csproj
dotnet run --project CentroMaritimo.UI/CentroMaritimo.UI.csproj

# Tests
dotnet test PuertoBB.Tests/PuertoBB.Tests.csproj
```

Cada app crea su base SQLite en `%LocalAppData%\PuertoBB\<App>`. Los servicios mock se controlan con
los flags `MailMockService` y `AfipMockService` (ambos default `false`) en `appsettings.json`; el modo
demo (seed + rótulo "MODO DEMO") se activa si cualquiera está en `true`. Estado de implementación y pendientes:
[`doc/decisiones/estado-implementacion.md`](doc/decisiones/estado-implementacion.md).

Skills útiles: `/validar-plataforma` (validación integral), `/testing` (guía de tests),
`/desarrollador`, `/diseño-wpf`, `/arquitecto`, `/investigador-afip`.

## Distribución

Para empaquetar cada app como un **único `.exe`** (autoejecutable single-file, framework-dependent):

```powershell
pwsh ./publish.ps1   # genera dist\CamaraPortuaria\*.exe y dist\CentroMaritimo\*.exe
```

El `.exe` distribuido va **sin `appsettings.json`** (excluido del publish con
`CopyToPublishDirectory=Never`), por lo que corre en **producción** por los defaults (ambos
mocks en `false`). Requiere en la PC destino el **.NET 10 Desktop Runtime (x64)** y el
**WebView2 Runtime** (incluido en Windows 11). Puesta en marcha:
[`doc/usuario/paso-a-produccion.md`](doc/usuario/paso-a-produccion.md).

## Documentación

Toda la documentación vive en [`doc/`](doc/): negocio, arquitectura, diseño, usuario, decisiones.

- **Integraciones externas (base de conocimiento reutilizable):** [`doc/integraciones/`](doc/integraciones/)
  — [AFIP/ARCA](doc/integraciones/afip.md) (`Afip.Net`: WSAA/WSFE/padrón) y [correo SMTP + OAuth2](doc/integraciones/correo.md).
- **Auditoría pre-producción:** [`doc/auditoria/auditoria-pre-produccion.md`](doc/auditoria/auditoria-pre-produccion.md).
- **Decisiones de diseño:** [`doc/decisiones/registro-decisiones.md`](doc/decisiones/registro-decisiones.md) (D-01…D-28).

## Stack

- .NET 10 · WPF · EF Core + SQLite · QuestPDF · MailKit · AFIP WSFE (SOAP)

## Arquitectura

```text
UI → Core ← Services
UI → Core ← Infrastructure
```

Core no depende de ningún otro proyecto.
