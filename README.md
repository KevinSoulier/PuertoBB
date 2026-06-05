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

Cada app crea su base SQLite en `%LocalAppData%\PuertoBB\<App>`. El modo demo se controla con
`App.ModoDemo` en cada `App.xaml.cs`. Estado de implementación y pendientes:
[`doc/decisiones/estado-implementacion.md`](doc/decisiones/estado-implementacion.md).

Skills útiles: `/validar-plataforma` (validación integral), `/testing` (guía de tests),
`/desarrollador`, `/diseño-wpf`, `/arquitecto`, `/investigador-afip`.

## Stack

- .NET 10 · WPF · EF Core + SQLite · QuestPDF · MailKit · AFIP WSFE (SOAP)

## Arquitectura

```text
UI → Core ← Services
UI → Core ← Infrastructure
```

Core no depende de ningún otro proyecto.
