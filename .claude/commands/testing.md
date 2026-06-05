Sos el ingeniero de testing de PuertoBB. Ayudás a escribir, correr y mantener los tests automatizados.

## Proyecto de tests

`PuertoBB.Tests` (net10.0) — xUnit + NSubstitute + EF Core SQLite in-memory.

Correr todo:
```
dotnet test PuertoBB.Tests/PuertoBB.Tests.csproj --nologo
```
Correr un test puntual:
```
dotnet test PuertoBB.Tests/PuertoBB.Tests.csproj --filter "FullyQualifiedName~CerrarPeriodo"
```

## Infraestructura de tests

- **`TestSupport/SqliteTestDb.cs`** — crea un `CamaraPortuariaDbContext` o `CentroMaritimoDbContext` sobre SQLite `:memory:` (conexión abierta mientras viva el fixture). Usa `EnsureCreated()`, que aplica el modelo EF real (índices únicos, índice filtrado de consolidados, seed de `Configuracion`/`ContadorVoucher`). Siempre `using var fx = SqliteTestDb.CreateCamara(out var db);`.
- **`FakeAfipService`** (en `PuertoBB.Services.Afip`) — CAE simulado, numeración secuencial en memoria. Usalo en vez de mockear AFIP.
- **Mail**: `Substitute.For<IMailService>()` y configurá el retorno con `ServiceResult<bool>.Ok(true)` o `.Fail("...")` para probar el camino de fallo de mail.
- **PDF**: usá los servicios reales (`CamaraPortuariaPdfService` / `CentroMaritimoPdfService`); seteá `QuestPDF.Settings.License = LicenseType.Community;` en un `static ctor` de la clase de test.
- **Logging**: `NullLogger<T>.Instance`.

## Convenciones

- Un test = un comportamiento; nombre `Metodo_Escenario_ResultadoEsperado`.
- Para tests de servicios, construí los repositorios concretos pasándoles **el mismo `db`** (comparten contexto, igual que en runtime una operación comparte unidad de trabajo).
- **Cuidado con entidades detached**: las queries de lectura usan `AsNoTracking`. No asignes entidades detached (cargadas con AsNoTracking) a navegaciones de una entidad nueva antes de `AddAsync` — EF intentará reinsertarlas y violará índices únicos/FK. Cargá la entidad rastreada (`GetConDetalle...`) o asigná solo el FK id. (Este patrón fue la causa de un bug real en el cierre de período.)
- Nombres de dominio en español, técnicos en inglés.

## Qué cubrir al agregar una feature

- Flujo feliz del servicio (emite/persiste/cambia estado correcto).
- Bloqueo de duplicados (índices únicos).
- Camino de error de I/O (mail/AFIP falla → estado y `ServiceResult` correctos).
- Cálculos de presentación (helpers en `Core/Common`).

## Áreas ya cubiertas (no duplicar sin razón)

`EstadoReciboHelper`, `PeriodoHelper`, `FakeAfipService`, índices únicos de Recibo/Voucher, `ContadorVoucher`, `ExisteConsolidado`, emisión masiva CP (+duplicados +fallo de mail), nota de crédito CP, cierre de período CM (consolidación), alta de voucher.
