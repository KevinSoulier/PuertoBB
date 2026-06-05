# Estructura de la solución

## Proyectos

| Proyecto | Tipo | Framework | Descripción |
| --- | --- | --- | --- |
| `PuertoBB.Core` | Class Library | net10.0 | Entidades, interfaces, enums, excepciones, ServiceResult |
| `PuertoBB.Services` | Class Library | net10.0 | AFIP/ARCA, PDF (QuestPDF), Mail (MailKit) |
| `PuertoBB.Infrastructure` | Class Library | net10.0 | EF Core, SQLite, repositorios, migraciones |
| `CamaraPortuaria.UI` | WPF App | net10.0-windows | App de la Cámara Portuaria |
| `CentroMaritimo.UI` | WPF App | net10.0-windows | App del Centro Marítimo |

## Referencias entre proyectos

```
CamaraPortuaria.UI  →  PuertoBB.Core
CamaraPortuaria.UI  →  PuertoBB.Services
CamaraPortuaria.UI  →  PuertoBB.Infrastructure

CentroMaritimo.UI   →  PuertoBB.Core
CentroMaritimo.UI   →  PuertoBB.Services
CentroMaritimo.UI   →  PuertoBB.Infrastructure

PuertoBB.Services       →  PuertoBB.Core
PuertoBB.Infrastructure →  PuertoBB.Core
```

**Core no depende de nada.**

## Paquetes NuGet por proyecto

**PuertoBB.Infrastructure:**
- `Microsoft.EntityFrameworkCore`
- `Microsoft.EntityFrameworkCore.Sqlite`
- `Microsoft.EntityFrameworkCore.Design`
- `System.ServiceModel.Http` *(cliente SOAP para AFIP WSFE/WSAA)*
- `System.ServiceModel.Security`

**PuertoBB.Services:**
- `QuestPDF`
- `MailKit`

**CamaraPortuaria.UI / CentroMaritimo.UI:**
- `Microsoft.Extensions.DependencyInjection`
- `Microsoft.Extensions.Hosting`
- `Serilog` *(logging)*
- `Serilog.Extensions.Hosting` *(integración con IHostBuilder)*
- `Serilog.Sinks.File` *(archivo de log diario rotativo)*
- `Serilog.Enrichers.Environment` *(enriquece logs con nombre de máquina)*

## Bases de datos

Cada entidad tiene su propio archivo SQLite:
- `camara-portuaria.db` → `CamaraPortuariaDbContext`
- `centro-maritimo.db` → `CentroMaritimoDbContext`
