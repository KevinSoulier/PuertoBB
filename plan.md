# Plan de trabajo — PuertoBB

> Plan vivo de la implementación end-to-end. Marca el estado real y el orden de trabajo.
> Última actualización: 2026-06-05 (sesión nocturna autónoma).

## Objetivo

Llevar PuertoBB de "documentación + esqueleto vacío" a una **plataforma de escritorio funcional
de punta a punta**: dos apps WPF (Cámara Portuaria y Centro Marítimo) que emiten recibos con CAE de
AFIP, generan PDF, envían mail, y gestionan empresas/agencias/grupos/vouchers, con tests y validación.

## Apps de referencia en el equipo (mismo `~/source/repos`)

| Proyecto | Qué aporta como ejemplo |
|---|---|
| `../CamaraPortuariaBB/CamaraPortuariaBB.App` | Patrón WPF Fluent + `NavigationService` + `MainWindowViewModel` + `DialogBox` estilo WPFGallery. **Confirma** el patrón que ya seguimos. |
| `../FacturadorAfip/FacturadorAfip.AfipWsfeClient` | **Cliente AFIP real**: `LoginCmsClient` (WSAA), `WsfeClient` (WSFE), `WsaaTicket`, `X509CertificateManager`, y referencias SOAP generadas (Connected Services). Referencia directa para implementar el cliente real. |
| `../WPF-Samples` (WPFGallery) | Fuente original del patrón de navegación (ya citado en `doc/diseño/fluent-navigation.md`). |

---

## FASE 1 — Plataforma base (✅ COMPLETADA)

Build verde, 18 tests verdes, smoke test de runtime OK en ambas apps. Detalle en
`doc/decisiones/estado-implementacion.md`.

- [x] **Core**: entidades CP/CM, enums, `ServiceResult`, modelos AFIP/Mail, DTOs, helpers, interfaces.
- [x] **Infrastructure**: DbContexts, configs EF (índices únicos + parcial de consolidados, seeds), `RepositoryBase` + repos, migraciones iniciales, DI.
- [x] **Services**: PDF (QuestPDF), Mail (MailKit + Fake), AFIP (orquestación WSAA/WSFE contra abstracción + `FakeAfipService`), servicios de negocio (emisión, NC, cierre de período, vouchers).
- [x] **UI** (ambas apps): shell Fluent (sidebar+Frame+nav DI), diálogos overlay, Serilog, handlers globales, migración+seed, tema; todas las páginas y ViewModels.
- [x] **Tests**: xUnit + NSubstitute + SQLite in-memory (helpers, repos, flujos completos).
- [x] **Skills**: `/validar-plataforma`, `/testing`. **Docs**: `doc/decisiones/`.

---

## FASE 2 — Integración AFIP real (✅ COMPLETADA)

Clientes SOAP concretos implementados detrás de `IWsaaClient` / `IWsfeClient`, en `PuertoBB.Services/Afip/Soap/`
(donde vive `AfipService`, consistente con el README que asigna AFIP a Services). Referencia: `FacturadorAfip.AfipWsfeClient`.

- [x] **2.1** Clientes SOAP generados con `dotnet-svcutil` desde los WSDL (había red): WSAA (`LoginCMSClient`) y WSFE v1 (`ServiceSoapClient`) en `Afip/Soap/Generated/`. Paquetes `System.ServiceModel.Http` + `.Primitives` (4.10.*).
- [x] **2.2** Firma CMS PKCS#7 ya en `TraBuilder` (con `MachineKeySet|PersistKeySet|Exportable`).
- [x] **2.3** `WsaaSoapClient : IWsaaClient` — llama `loginCms` y devuelve el XML del TA (parseo de token/sign en `AfipService`).
- [x] **2.4** `WsfeSoapClient : IWsfeClient` — `FEDummy`, `FECompUltimoAutorizado`, `FECAESolicitar`. Mapeo en `WsfeMapper` (**sin array IVA** para tipo C, `CbtesAsoc` en NC, importes exentos).
- [x] **2.5** Clientes reales registrados en DI cuando `usarFake=false` (`AddPuertoBBAfip(false)`).
- [x] **2.6** Tests de mapeo (request exento C / NC con asociado, respuesta aprobada/rechazada) y de `TraBuilder`. Total: **23 tests verdes**. (Llamada real no testeada: requiere red+certificado.)
- [x] **2.7** Documentado en `doc/arquitectura/afip-integracion.md` y `doc/decisiones/`.

## FASE 3 — Pulido y features menores (🔄 en curso)

- [x] **Backup manual de la base SQLite desde la UI** (mencionado en negocio): `IBackupService` (Core) +
  `BackupService` por app (VACUUM INTO, copia consistente), botón en Configuración, test del VACUUM INTO.
  Total tests: **24 verdes**.
- [ ] Revisar `CamaraPortuariaBB.App` (NavigationService/DialogBox) y portar mejoras menores si aplica.
- [ ] `/code-review` sobre el diff y aplicar limpiezas seguras.
- [ ] Verificar consistencia visual CP/CM (mismo layout, distinto acento).

## Reglas de trabajo durante la noche

- Mantener **build verde y tests verdes** tras cada bloque (compilar seguido).
- No romper las reglas de arquitectura (Core sin deps, sin MessageBox, sin Console.WriteLine, VMs sin DbContext).
- Documentar cada decisión nueva en `doc/decisiones/registro-decisiones.md`.
- No commitear salvo que el usuario lo pida; dejar el árbol coherente.
- Si una sub-tarea requiere red/credenciales no disponibles, implementar todo lo posible y dejarlo documentado como paso manual (no inventar resultados).
