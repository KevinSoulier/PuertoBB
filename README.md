# PuertoBB

Sistema de gestión de recibos para dos entidades portuarias de Bahía Blanca.

## Entidades

- **Cámara Portuaria** — emisión de recibos a empresas con cuota social y cobros extraordinarios
- **Centro Marítimo** — emisión de recibos a agencias con vouchers por ingreso de buques

## Proyectos

| Proyecto | Tipo | Descripción |
| --- | --- | --- |
| `PuertoBB.Core` | Class Library | Entidades, interfaces, enums, excepciones |
| `PuertoBB.Services` | Class Library | AFIP/ARCA, generación de PDF, envío de mail |
| `PuertoBB.Infrastructure` | Class Library | EF Core, SQLite, repositorios |
| `CamaraPortuaria.UI` | WPF App | Interfaz de la Cámara Portuaria |
| `CentroMaritimo.UI` | WPF App | Interfaz del Centro Marítimo |

## Stack

- .NET 10 · WPF · EF Core + SQLite · QuestPDF · MailKit · AFIP WSFE (SOAP)

## Arquitectura

```text
UI → Core ← Services
UI → Core ← Infrastructure
```

Core no depende de ningún otro proyecto.
