# Plan de Fixing — Auditoría 2026-06-10 (runbook para agente económico)

> **Cómo ejecutarlo:** en una sesión nueva: *"Ejecutá el plan de fixing de
> `doc/auditoria/plan-fixing-2026-06-10.md`"*. Diseñado para correr con un modelo económico
> (sonnet): las decisiones de diseño ya están tomadas y escritas acá — el ejecutor APLICA, no
> diseña. Se puede correr por etapas (*"…solo Etapas 1 a 3"*).

**Insumo de referencia:** `doc/auditoria/AUDITORIA-2026-06-10.md` (los IDs P0/P1/P2/P3 vienen de ahí).

## Reglas duras para el ejecutor (leer dos veces)

1. **No improvisar.** Si el código real no coincide con lo que describe un ítem (el método no
   existe, la línea cambió, la firma es otra), NO adaptar el fix: marcar el ítem como ⏭️
   SALTADO con el motivo en el log y seguir con el siguiente.
2. **Log de ejecución obligatorio:** mantener `doc/auditoria/fixing-log.md` con una línea por
   ítem: `✅ hecho (commit) / ⏭️ saltado (motivo) / ❌ falló (detalle)`.
3. Rama `fix/auditoria-2026-06-10` desde `main`. **Un commit por ítem o grupo de ítems**, con
   el ID en el mensaje (ej.: `fix(P0-1): no asignar navegación Empresa al crear recibo`).
4. Después de CADA ítem: `dotnet build PuertoBB.slnx` → debe dar 0 warnings/0 errores.
   Después de cada etapa: `dotnet test PuertoBB.slnx` → todo verde. **Nunca commitear en rojo.**
5. **Cero refactors fuera de lo prescripto.** No renombrar, no "mejorar de paso", no tocar
   `Afip.Net/Soap/Generated`.
6. **Paridad CP/CM:** cuando el ítem diga "(ambas apps)" o "(ambos servicios)", aplicar el
   cambio espejado en CamaraPortuaria y CentroMaritimo con los renombres Empresa↔Agencia.
7. **Migraciones:** NO crear migraciones incrementales en ningún ítem. Todos los cambios de
   esquema se acumulan y la migración `Inicial` de cada contexto se REGENERA una sola vez en
   el ítem 4.6 (convención del proyecto: una migración por release).
8. Ítems marcados **[DIFERIDO]** no se ejecutan en esta corrida (requieren diseño o decisión
   del usuario): dejarlos anotados en el log y no tocarlos.

---

## Etapa 1 — P0-1: emisión CP rota

### 1.1 Test de regresión PRIMERO (debe quedar ROJO antes del fix)

En `PuertoBB.Tests`, junto al harness existente (`TestSupport/SqliteTestDb.cs`,
`ServiceFlowTests.BuildService`), agregar un builder alternativo que arme
`CamaraPortuariaReciboService` con **un DbContext NUEVO por repositorio**, todos sobre la
MISMA `SqliteConnection` del fixture (así replica el registro Transient real de las apps):

```csharp
private static CamaraPortuariaDbContext NuevoContexto(SqliteTestDb fixture)
{
    var options = new DbContextOptionsBuilder<CamaraPortuariaDbContext>()
        .UseSqlite(fixture.Connection).Options;   // exponer la connection del fixture si hace falta
    return new CamaraPortuariaDbContext(options);
}

private static CamaraPortuariaReciboService BuildServiceContextosSeparados(SqliteTestDb fixture, IMailService mail, IAfipService afip)
    => new(
        new CpRepos.ReciboRepository(NuevoContexto(fixture), NullLogger<CpRepos.ReciboRepository>.Instance),
        new CpRepos.GrupoFacturacionRepository(NuevoContexto(fixture), NullLogger<CpRepos.GrupoFacturacionRepository>.Instance),
        new CpRepos.EmpresaRepository(NuevoContexto(fixture), NullLogger<CpRepos.EmpresaRepository>.Instance),
        new CpRepos.NotaDeCreditoRepository(NuevoContexto(fixture), NullLogger<CpRepos.NotaDeCreditoRepository>.Instance),
        new CpRepos.ConfiguracionRepository(NuevoContexto(fixture)),
        afip, /* pdf fake como en BuildService */ , mail,
        NullLogger<CamaraPortuariaReciboService>.Instance);
```

Test `EmitirMasivo_ConContextosSeparados_EmiteSinReinsertarEmpresa`: sembrar (con un contexto
propio) 1 empresa con 1 email + 1 grupo con la empresa + Configuracion/PuntoDeVenta como hacen
los tests existentes; llamar `EmitirMasivoAsync(grupoId, 2026, 6)`; afirmar que el resultado
por entidad es `Exito == true` y que la tabla `Empresas` sigue teniendo exactamente 1 fila.
Correrlo: **debe fallar** (hoy devuelve fallo "No se pudo guardar Recibo…"). Crear el test
gemelo para CM (`CentroMaritimoReciboService`) que debe pasar ya mismo (CM no tiene el bug).

### 1.2 El fix

`PuertoBB.Services/Negocio/CamaraPortuariaReciboService.cs`, método `EmitirOResumirAsync`,
en el `new Recibo { ... }` (~línea 168): **eliminar la línea `Empresa = empresa,`** (dejar
`EmpresaId = empresa.Id`). No tocar nada más del objeto. Es el espejo exacto de
`ConstruirRecibo` de CM (CentroMaritimoReciboService.cs:392-394, ver su comentario).
Re-correr el test de 1.1: ahora verde. Correr la suite completa.

---

## Etapa 2 — Transaccionalidad fiscal (P1-1, P1-2)

### 2.1 P1-1 · Cierre consolidado CM: Pendiente-first + un solo SaveChanges

**a)** En `PuertoBB.Core/Interfaces/Repositories/CentroMaritimo/IReciboRepository.cs` agregar:

```csharp
/// <summary>Recibo consolidado del período, con Vouchers/Lineas/Agencia.Emails. Excluye Anulados.</summary>
Task<Recibo?> GetConsolidadoAsync(int agenciaId, int anio, int mes, CancellationToken ct = default);
/// <summary>Persiste el recibo y vincula los vouchers en UN solo SaveChanges (atómico).</summary>
Task AddConVouchersAsync(Recibo recibo, IReadOnlyList<int> voucherIds, CancellationToken ct = default);
```

**b)** Implementar en `PuertoBB.Infrastructure/Repositories/CentroMaritimo/ReciboRepository.cs`:

```csharp
public Task<Recibo?> GetConsolidadoAsync(int agenciaId, int anio, int mes, CancellationToken ct = default)
    => _db.Recibos
        .Include(r => r.Agencia).ThenInclude(a => a.Emails)
        .Include(r => r.Vouchers).ThenInclude(v => v.Barco)
        .Include(r => r.Lineas)
        .FirstOrDefaultAsync(r => r.AgenciaId == agenciaId && r.EsConsolidadoVouchers &&
                                  r.PeriodoAnio == anio && r.PeriodoMes == mes &&
                                  r.Estado != Core.Enums.ReciboEstado.Anulado, ct);

public async Task AddConVouchersAsync(Recibo recibo, IReadOnlyList<int> voucherIds, CancellationToken ct = default)
{
    recibo.CreatedAt = DateTime.Now;
    _db.Recibos.Add(recibo);
    var vouchers = await _db.Vouchers.Where(v => voucherIds.Contains(v.Id)).ToListAsync(ct);
    foreach (var v in vouchers) v.Recibo = recibo;   // mismo contexto: FK se resuelve al guardar
    await GuardarAsync(ct);                          // un único SaveChanges
}
```

(`GuardarAsync` es el helper de `RepositoryBase`; si no es accesible, replicar su try/catch de
`DbUpdateException` → `ReciboException`.)

**c)** Reescribir `ProcesarCierreAgenciaAsync` (CentroMaritimoReciboService.cs:115-175) a este flujo:

```
1. var existente = await _recibos.GetConsolidadoAsync(agenciaId, anio, mes, ct);
2. Si existente != null:
   a. Si EsCompleto(existente) → return Omitida("Ya existe un recibo consolidado para este período.");
   b. Si string.IsNullOrEmpty(existente.CAE) (Pendiente): re-sincronizar —
      vincular los pendientes nuevos (AddConVouchersAsync NO sirve acá porque el recibo ya existe:
      usar _vouchers.MarcarConsolidadosAsync(idsNuevos, existente.Id, ct) si hay pendientes nuevos),
      y rearmar existente.Importe / existente.Detalle / existente.Lineas a partir de
      existente.Vouchers ∪ pendientes nuevos (misma proyección de líneas que el flujo actual:
      "Voucher {n} — {barco} — {fecha}"). Después: return await ProcesarReciboAsync(existente, agencia, config, enviarMail, ct);
   c. Si tiene CAE (Emitido con mail fallido) → return await ProcesarReciboAsync(existente, agencia, config, enviarMail, ct);
3. Si existente == null (caso nuevo):
   a. Cargar agencia (igual que hoy, GetConDetalleAsync; si null → Fallo).
   b. recibo = ConstruirRecibo(..., esConsolidado: true) PERO con Estado = ReciboEstado.Pendiente
      y SIN pedir CAE todavía. Mantener el armado de Lineas por voucher tal como está hoy (líneas 135-144).
   c. await _recibos.AddConVouchersAsync(recibo, vouchersAgencia.Select(v => v.Id).ToList(), ct);
   d. return await ProcesarReciboAsync(recibo, agencia, config, enviarMail, ct);
```

`ProcesarReciboAsync` ya es idempotente (pide CAE solo si falta, maneja consolidados en el
mail vía `EsConsolidadoVouchers`) — NO modificarlo, solo reutilizarlo. Eliminar las llamadas
viejas a `EmitirCaeAsync`/`AplicarCae`/`AddAsync`/`MarcarConsolidadosAsync` de este método y
el bloque `if (enviarMail)` manual (líneas 154-166): todo eso lo hace `ProcesarReciboAsync`.
`ExisteConsolidadoAsync` deja de usarse en este método (puede quedar para otros usos).

**d)** Tests nuevos en `ServiceFlowTests` (con el harness actual de contexto compartido):
- `CerrarPeriodo_FallaCae_PersisteReciboPendienteYVouchersVinculados`: AFIP fake que falla →
  el recibo queda `Pendiente` sin CAE, los vouchers con `ReciboId` asignado.
- `CerrarPeriodo_ReintentoTrasFalloCae_CompletaSinDuplicar`: segundo llamado → mismo recibo
  (1 solo consolidado), ahora con CAE.

### 2.2 P1-2 · Anulación atómica (ambas apps)

**a)** Agregar a ambos `IReciboRepository` (CP y CM):

```csharp
/// <summary>Marca el recibo Anulado y persiste la NC en UN solo SaveChanges. En CM además desvincula los vouchers del consolidado.</summary>
Task AnularConNotaAsync(Recibo recibo, NotaDeCredito nota, CancellationToken ct = default);
```

**b)** Implementación CP (`CamaraPortuaria/ReciboRepository.cs`):

```csharp
public async Task AnularConNotaAsync(Recibo recibo, NotaDeCredito nota, CancellationToken ct = default)
{
    recibo.Estado = Core.Enums.ReciboEstado.Anulado;
    recibo.UpdatedAt = DateTime.Now;
    nota.CreatedAt = DateTime.Now;
    _db.Set<NotaDeCredito>().Add(nota);
    await GuardarAsync(ct);
}
```

Implementación CM: ídem MÁS, antes del `GuardarAsync`:

```csharp
if (recibo.EsConsolidadoVouchers)
    foreach (var v in recibo.Vouchers) v.ReciboId = null;   // P1-3: libera los vouchers para reemisión
```

(El `recibo` llega trackeado por este mismo contexto porque viene de `GetConDetalleAsync` del
mismo repositorio, que ya incluye `Vouchers` — verificarlo; si el call-site usara otra fuente,
SALTAR y anotar.)

**c)** En ambos `AnularReciboAsync` (CP:353-356, CM:543-546): reemplazar el par
`recibo.Estado = Anulado; await _recibos.UpdateAsync(...); await _notas.AddAsync(nota, ct);`
por `await _recibos.AnularConNotaAsync(recibo, nota, ct);`. La inyección de `_notas` queda sin
ese uso; si no le quedan otros usos en el servicio, dejarla igual (no refactorizar firmas).

**d)** Test: `Anular_PersisteReciboYNotaJuntos` (verificar que tras anular existen el estado
Anulado Y la NC; simular fallo de guardado con un recibo inválido es opcional — si es
complicado, omitir y anotarlo).

---

## Etapa 3 — Reemisión tras anular + clave individual (P1-3, P1-4)

### 3.1 P1-3 (lo que falta; la desvinculación de vouchers ya entró en 2.2b)

- `CentroMaritimo/ReciboRepository.ExisteConsolidadoAsync` (líneas 50-55): agregar
  `&& r.Estado != Core.Enums.ReciboEstado.Anulado` al predicado.
- `CentroMaritimo/ReciboConfiguration.cs` (líneas 30-32): cambiar el filtro del índice único a
  `.HasFilter("\"EsConsolidadoVouchers\" = 1 AND \"Estado\" <> 'Anulado'")`.
  (Cambio de esquema → se materializa en 4.6, no crear migración acá.)
- Test: `AnularConsolidado_PermiteReemitirElPeriodo` — cerrar período, anular el consolidado,
  volver a cerrar → nuevo recibo consolidado OK y vouchers vinculados al nuevo.

### 3.2 P1-4 · Clave de emisión individual (ambas apps)

Decisión ya tomada (no re-debatir): **se permiten N recibos individuales por (entidad,
período)**; `GetPorClaveAsync(entidad, grupoId: null, …)` pasa a significar "individual
RETOMABLE del período" (solo `Pendiente`), y en CM nunca matchea consolidados.

En `FiltrarPorClave` de ambos `ReciboRepository`, reemplazar la rama `grupoId == null`:

```csharp
// CP (ReciboRepository.cs:34-40):
return grupoId is int gid
    ? q.Where(r => r.EmisionGrupo != null && r.EmisionGrupo.GrupoFacturacionId == gid)
    : q.Where(r => r.EmisionGrupo == null && r.Estado == Core.Enums.ReciboEstado.Pendiente);

// CM (ReciboRepository.cs:36-42): ídem + excluir consolidados:
    : q.Where(r => r.EmisionGrupo == null && !r.EsConsolidadoVouchers
                   && r.Estado == Core.Enums.ReciboEstado.Pendiente);
```

Efecto en `EmitirOResumirAsync` (sin tocarlo): si hay un individual Pendiente del período lo
retoma (re-sync + CAE); si todos los individuales están completos, crea uno nuevo. La rama
`EsCompleto → "Ya existe"` queda inalcanzable para individuales — dejarla (sigue valiendo
para grupos).

Tests: `EmitirIndividual_DosVecesMismoPeriodo_CreaDosRecibos`;
`EmitirIndividual_ConPendientePrevio_RetomaSinDuplicar`;
(CM) `EmitirIndividual_EnPeriodoConsolidado_NoChocaConElConsolidado`.

Documentar la decisión: crear `doc/decisiones/` el archivo con el próximo número libre
(mirar la carpeta; ej. `D-17-clave-emision-individual.md`) con 5-10 líneas.

---

## Etapa 4 — Resto de P1

### 4.1 P1-5 + P2-9 · BackupService (ambas apps)

- `RestaurarAsync`: después de `_db.Database.CloseConnection();` agregar
  `Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();`. Cambiar el mensaje de éxito a
  indicar "Base restaurada. Cierre y vuelva a abrir la aplicación." (y si el call-site del
  ViewModel muestra un diálogo, que use ese texto).
- `BackupAsync`: antes del `VACUUM INTO`, `if (File.Exists(destinoPath)) File.Delete(destinoPath);`.

### 4.2 P1-6 + P3-3 + P3-8 · App.xaml.cs (ambas apps)

- `OnStartup`: envolver TODO el cuerpo (después de `base.OnStartup(e)`) en
  `try { … } catch (Exception ex) { MessageBox.Show($"Error al iniciar la aplicación:\n\n{ex.Message}", "Error de inicio", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1); }`.
- `InicializarBaseDeDatosAsync`: resolver el DbContext dentro de
  `await using var scope = _host.Services.CreateAsyncScope();` usando `scope.ServiceProvider`.
- `OnExit`: envolver el `StopAsync` en try/catch con `_logger?.LogError(ex, "Error en OnExit")`.

### 4.3 P1-7 · PDF de NC: comprobante asociado

En `Afip.Documentos/Pdf/ComprobanteTemplate.cs`, dentro del cuerpo (después de la sección del
receptor y antes de la tabla/detalle), agregar bloque condicional:

```csharp
if (_doc.ComprobanteAsociado is { } ca)
    col.Item().PaddingTop(4).Text(t =>
    {
        t.Span("Comprobante asociado: ").SemiBold();
        t.Span($"{TipoComprobanteAfip.Nombre(ca.Tipo)} {TipoComprobanteAfip.Letra(ca.Tipo)} — {ca.PuntoVenta:0000}-{ca.Numero:00000000}");
    });
```

Ajustar nombres reales de las propiedades de `Afip.Documentos/Models/ComprobanteAsociado.cs`
y de los helpers del catálogo ANTES de aplicar (si difieren, adaptar SOLO los nombres; si la
estructura es otra, SALTAR y anotar). Test en `Afip.Documentos.Tests`: generar una NC con
`ComprobanteAsociado` poblado → PDF válido (> 1000 bytes, empieza con `%PDF`), sin excepción.

### 4.4 P1-8 · Ingresos Brutos + Inicio de Actividades

1. En ambas entidades `Configuracion` (CP y CM): `public string? IngresosBrutos { get; set; }`
   y `public DateTime? InicioActividades { get; set; }`.
2. En ambas `ConfiguracionConfiguration`: `b.Property(c => c.IngresosBrutos).HasMaxLength(50);`.
   NO tocar el `HasData` (los campos nuevos quedan null en el seed).
3. En ambos PdfService, al construir `EmisorDocumento` (CP: `GenerarPdfReciboAsync` y
   `GenerarPdfNotaDeCreditoAsync` inline; CM: `BuildEmisor`): asignar
   `IngresosBrutos = config.IngresosBrutos` y `InicioActividades = config.InicioActividades`
   (verificar nombres de las propiedades en `EmisorDocumento`; el template ya las renderiza si
   no son null).
4. UI (ambas apps): en la pestaña de datos del emisor de `ConfiguracionPage.xaml` agregar dos
   campos (TextBox para IIBB, DatePicker para Inicio de Actividades) bindeados a dos
   propiedades nuevas del `ConfiguracionViewModel`, siguiendo EXACTAMENTE el patrón de las
   propiedades vecinas existentes (`RazonSocial`/`Cuit`: snapshot para cancelar + guardado en
   el mismo comando). No inventar layout nuevo: copiar el patrón de una fila existente.

### 4.5 P1-9 · ModoDemo configurable

1. En ambas apps crear `appsettings.json` (raíz del proyecto UI, `CopyToOutputDirectory=PreserveNewest` en el csproj):
   `{ "PuertoBB": { "ModoDemo": true, "Afip": "Mock" } }`.
2. En `App.xaml.cs`: cambiar `public const bool ModoDemo` / `public const AfipModo Afip` por
   `public static bool ModoDemo { get; private set; } = true;` (ídem Afip). En `OnStartup`,
   después de construir `_host`, leerlos de `_host.Services.GetRequiredService<IConfiguration>()`…
   ⚠️ PROBLEMA: `ConfigureServices` los usa ANTES de tener el host. Solución prescripta: leer
   el JSON a mano ANTES del builder:
   ```csharp
   var cfg = new ConfigurationBuilder()
       .SetBasePath(AppContext.BaseDirectory)
       .AddJsonFile("appsettings.json", optional: true).Build();
   ModoDemo = cfg.GetValue("PuertoBB:ModoDemo", true);
   Afip     = Enum.TryParse<AfipModo>(cfg["PuertoBB:Afip"], out var m) ? m : AfipModo.Mock;
   ```
   y recién después `Host.CreateDefaultBuilder()...`. Quitar el `#pragma warning disable CS0162`
   (ya no hay rama inalcanzable).
3. Banner: en `MainWindow` (ambas), si `App.ModoDemo`, anexar " — MODO DEMO" al título de la
   ventana. Nada más elaborado.

### 4.6 Regenerar la migración única (cierra Etapas 3 y 4)

1. Borrar el contenido de `PuertoBB.Infrastructure/Migrations/CamaraPortuaria/` y
   `/CentroMaritimo/` (dejar `.gitkeep` si existía).
2. `dotnet ef migrations add Inicial --project PuertoBB.Infrastructure --context CamaraPortuariaDbContext`
   e ídem `--context CentroMaritimoDbContext` (mismo modo en que se generaron antes: sin
   `--startup-project`, el Design package está en Infrastructure).
3. `dotnet ef migrations has-pending-model-changes` para ambos → limpio.
4. Borrar las `.db` de dev (`%LOCALAPPDATA%/PuertoBB/*/**.db`) para que el próximo arranque migre de cero.
5. `dotnet test` completo.

---

## Etapa 5 — P2 (one-liners primero)

| Ítem | Cambio prescripto |
|---|---|
| P2-6 | `ControlPagosViewModel.cs:21` (ambas): `set { if (SetField(ref _soloVencidos, value)) _ = BuscarAsync(); }` (copiar el patrón de `IncluirMorosos` 3 líneas abajo). |
| P2-13 | En ambos `ReenviarMailAsync`, tras el null-check del recibo: `if (string.IsNullOrEmpty(recibo.CAE)) return ServiceResult<bool>.Fail("El recibo no tiene CAE: emítalo antes de reenviar.");` |
| P2-14 | En ambos servicios, donde se usa `config.PuntoDeVentaActivo?.Numero ?? 0` para EMITIR o ANULAR: antes, `if (config.PuntoDeVentaActivo is null) return …Fail("Configure un punto de venta activo en Configuración.");` (en `EmitirOResumirAsync` de CP devolver `ResultadoEmisionPorEntidad.Fallo(...)`; en CM en `ConstruirRecibo` no se puede devolver — poner el guard en los call-sites públicos que obtienen `config`). Quitar los `?? 0` que queden muertos. |
| P2-3 | `AfipService.cs:54`: reemplazar `resp.FechaVencimientoCae ?? request.FechaEmision.AddDays(10)` por: si `resp.FechaVencimientoCae is null` → loguear warning y usar `default` (DateTime.MinValue) — y en los PdfService, no imprimir vencimiento CAE si es `default`. Si esto último requiere tocar el template, SALTAR la parte del PDF y solo dejar el warning + default. |
| P2-4 | `TraBuilder.FirmarCms`: `using var cert = …` y reemplazar `MachineKeySet \| PersistKeySet \| Exportable` por `EphemeralKeySet`. Actualizar el comentario "SHA1" → "SHA256 (default de CmsSigner)". Correr los tests de Afip.Net si existen; si el login WSAA mock no usa esta ruta, anotar que requiere prueba real. |
| P2-1 | `EmisionMasivaViewModel` (ambas): campo `private CancellationTokenSource _cargarCts = new();` y en `CargarEstadoAsync` aplicar el patrón cancel-previo del informe (H-08, código incluido ahí). |
| P2-7 | `VouchersPage.xaml:129`: reemplazar el `TextBox` de Importe por el mismo control numérico que usa GruposPage para `PrecioUnitario` (`ui:NumberBox` u homólogo — copiar atributos de ahí). |
| P2-8 | Crear `PuertoBB.Core/Common/CuitValidator.cs`: `public static bool EsValido(string? cuit)` — normaliza con dígitos solos, exige 11, valida dígito verificador (pesos 5,4,3,2,7,6,5,4,3,2; resto 11−(suma%11); 11→0, 10→inválido). Tests con 3 CUITs válidos (usar los del SeedData) y 3 inválidos. Usarlo en `EmpresasViewModel.AceptarAsync` y `AgenciasViewModel.AceptarAsync`: `if (!CuitValidator.EsValido(CuitEdit)) { MostrarError("El CUIT no es válido."); return; }` |
| P2-11 | (ambas apps) En `ConfiguracionViewModel`, al guardar SMTP: `SmtpPassword = _protector.Protect(SmtpPassword)` en el objeto persistido (espejo de cómo se protege `CertificadoPassword` ~línea 513); en `MailConfigProvider.GetAsync`: `SmtpPassword = _protector.Unprotect(c.SmtpPassword)` (inyectar `ISecretProtector`). El prefijo `dpapi:` ya da migración suave para lo guardado en texto plano. ⚠️ cuidar el round-trip del PasswordBox (no re-proteger un valor ya protegido al re-guardar sin cambios: si el valor empieza con `"dpapi:"`, no volver a Protect). |
| P2-12 | Actualizar `doc/arquitectura/datos.md` y `flujos.md` y `doc/negocio/funcionalidad-compartida.md` al modelo real: `ReciboLinea`/`Lineas`, snapshot `Receptor*`, `EmisionGrupo` (Recibo ya no tiene `GrupoFacturacionId`), estado `Pendiente` (+ su color `#FFF3E0`), `UltimoErrorCae/Mail`, `FechaEnvioMail`, `EsMoroso`, entidad `PuntoDeVenta`, `ImporteVoucherPredeterminado`, campos nuevos de P1-8. Solo documentar lo que EXISTE en el código. |
| P2-5 | (si no entró en 4.6) Índice único de numeración en ambas `ReciboConfiguration`: `b.HasIndex(r => new { r.PuntoDeVenta, r.NumeroComprobante, r.CodigoAfip }).IsUnique().HasFilter("\"NumeroComprobante\" > 0");` → requiere repetir 4.6 (regenerar Inicial). Hacerlo ANTES de 4.6 idealmente. |
| P2-2 | **[DIFERIDO]** Reconciliación `FECompConsultar` tras timeout post-aprobación: requiere diseño en Afip.Net — no ejecutar con agente económico. |
| P2-10 | **[DIFERIDO — decisión del usuario]** Datos reales en SeedData: ¿anonimizar o externalizar? No tocar hasta que el usuario decida. |

## Etapa 6 — P3 mecánicos (los demás P3 del informe: NO tocarlos)

Solo estos, cada uno con su patrón ya descripto en el informe: P3-1, P3-2 (nuevo método de
repo una-query; incluir `Empresa.Emails`+`Lineas` en la query de EnviarMasivo), P3-9 (await
directo), P3-10 (reemplazo de colección + `OnPropertyChanged`), P3-11 (atributo de
virtualización), P3-12 (desuscribir en `Unloaded`), P3-16 (igualar mensajes al texto más
descriptivo de los dos), P3-18 (unificar formato del total). **[DIFERIDOS]:** P3-15 (portar
visor PDF embebido a CP) y todo P3 no listado acá.

## Etapa 7 — Verificación final

1. `dotnet build` (0 warnings) + `dotnet test` completo.
2. `/validar-plataforma`.
3. Smoke test runtime en modo demo (borrar `.db` viejas primero):
   - **CP:** emisión masiva de un grupo (valida el P0 en vivo), emisión individual ×2 mismo
     período, anular con NC, reenviar mail, marcar pagado, backup + restaurar + reabrir.
   - **CM:** alta de voucher, cierre de período, anular el consolidado y **reemitir el mismo
     período**, emisión individual en un mes ya cerrado, preview del PDF consolidado, PDF de
     una NC (verificar a ojo el "Comprobante asociado" y los campos IIBB si se cargaron).
4. Completar `doc/auditoria/fixing-log.md` y marcar en `AUDITORIA-2026-06-10.md` cada hallazgo
   ✅ corregido (con commit) / ⏭️ diferido. Listar al final los ítems SALTADOS para que el
   modelo principal los retome.
