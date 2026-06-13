# Plan de Fixing — Auditoría 2026-06-11 (runbook para agente económico)

> **Cómo ejecutarlo:** en una sesión nueva: *"Ejecutá el plan de fixing de
> `doc/auditoria/plan-fixing-2026-06-11.md`"*. Las decisiones de diseño ya están tomadas y
> escritas acá — el ejecutor APLICA, no diseña. Se puede correr por etapas
> (*"…solo Etapas 1 a 3"*). La Etapa 4 (Afip.Net) es la más delicada: si el ejecutor es un
> modelo económico y duda, puede saltarla completa y anotarla.

**Insumo de referencia:** `doc/auditoria/AUDITORIA-2026-06-11.md` (IDs N-1…N-12 y el
checklist de entrega vienen de ahí).

**Decisiones del usuario ya tomadas (no re-preguntar):**
- Distribución (ítem 1 del checklist): **FUERA de este plan** — no crear publish profiles ni
  instalador.
- Documentación de usuario (ítems 2 y 9): **SÍ entra** — Etapa 7.
- P2-10: cerrado — los datos reales del seed se quedan (registrar decisión, Etapa 5.6).

## Reglas duras para el ejecutor (leer dos veces)

1. **No improvisar.** Si el código real no coincide con lo que describe un ítem (método
   inexistente, línea corrida, firma distinta), NO adaptar el diseño: marcar ⏭️ SALTADO con
   el motivo en el log y seguir. Ajustar SOLO nombres triviales (typos, renames evidentes).
2. **Log de ejecución obligatorio:** `doc/auditoria/fixing-log-2026-06-11.md`, una línea por
   ítem: `✅ hecho / ⏭️ saltado (motivo) / ❌ falló (detalle)` + tamaño de suite tras cada etapa.
3. Rama `fix/auditoria-2026-06-11` desde `main`; un commit por ítem o grupo, con el ID en el
   mensaje (ej. `fix(N-1): no pisar individual Pendiente con contenido distinto`). Si git está
   bloqueado en la sesión (pasó el 06-10), trabajar sobre el working tree y anotarlo.
4. **⚠️ Build:** cerrar las apps y Visual Studio antes de compilar (los locks de VS rompen el
   build Debug con MSB3027). Si no se puede, usar `-c Release` consistentemente. Después de
   CADA ítem: build 0 warnings/0 errores. Después de cada etapa: `dotnet test` (los dos
   proyectos de test) todo verde. **Nunca avanzar en rojo.** Línea base actual: 98/98.
5. **Cero refactors fuera de lo prescripto.** No tocar `Afip.Net/Soap/Generated` (salvo que
   un ítem lo diga — ninguno lo dice: la referencia generada YA expone `FECompConsultarAsync`).
6. **Paridad CP/CM:** "(ambas apps)" = cambio espejado con renombres Empresa↔Agencia.
7. **Sin cambios de esquema.** Ningún ítem de este plan toca entidades persistidas ni
   configuración EF. Si algo pareciera requerir una migración → SALTAR y anotar (convención:
   una migración `Inicial` por release).
8. Tests nuevos: seguir el harness existente de `ServiceFlowTests.cs` (`SqliteTestDb`,
   `BuildService`, fakes de AFIP/Mail) y el patrón "contextos separados" ya presente
   (`EmitirMasivo_ConContextosSeparados_*`, ~línea 477 para CM).

---

## Etapa 1 — N-1 (P1): no pisar un individual Pendiente con contenido distinto (ambas apps)

### 1.1 Tests PRIMERO (deben quedar ROJOS)

`EmitirIndividual_PendientePrevioConContenidoDistinto_CreaReciboNuevo` (CP, y gemelo `_CM`):

1. Sembrar como los tests de individual existentes (`EmitirIndividual_DosVecesMismoPeriodo_*`).
2. AFIP fake en modo FALLA → `EmitirIndividualAsync(entidad, 5000m, "Papelería", hoy, 2026, 6, enviarMail: false)`
   → queda 1 recibo `Pendiente` con su línea "Papelería"/$5000.
3. AFIP fake en modo OK → `EmitirIndividualAsync(entidad, 20000m, "Cobro extraordinario", …mismo período…)`.
4. Asserts: **2 recibos** individuales del período; el primero sigue `Pendiente` con líneas
   originales (1 línea, "Papelería", Importe 5000); el segundo `Emitido` con CAE, 1 línea
   "Cobro extraordinario", Importe 20000.

Hoy el paso 3 pisa el snapshot del Pendiente y emite un único recibo → rojo. Verificar
también que `EmitirIndividual_ConPendientePrevio_RetomaSinDuplicar` (mismo contenido → retoma)
**sigue verde después del fix** — esa semántica no cambia.

### 1.2 El fix

En `CamaraPortuariaReciboService.EmitirOResumirAsync` (~línea 167) y
`CentroMaritimoReciboService.EmitirOResumirAsync` (~línea 320), inmediatamente después de
`var recibo = await _recibos.GetPorClaveAsync(...)`:

```csharp
// N-1: para individuales, un Pendiente con contenido DISTINTO no es un reintento — es otro
// cobro. Crear un recibo nuevo en lugar de pisar el snapshot (D-20 permite N por período).
if (recibo is not null && grupoId is null && !MismoContenido(recibo, lineas))
    recibo = null;
```

Helper privado en cada servicio (espejado; los tipos `Recibo`/`ReciboLinea` son por-app):

```csharp
private static bool MismoContenido(Recibo r, IReadOnlyList<ReciboLineaInput> lineas)
{
    if (r.Lineas.Count != lineas.Count) return false;
    var actuales = r.Lineas.OrderBy(l => l.Orden).ToList();
    for (var i = 0; i < lineas.Count; i++)
        if (actuales[i].Descripcion != lineas[i].Descripcion ||
            actuales[i].Cantidad != lineas[i].Cantidad ||
            actuales[i].PrecioUnitario != lineas[i].PrecioUnitario)
            return false;
    return true;
}
```

No tocar nada más: la rama de creación ya maneja `recibo == null`, y `GetPorClaveAsync`
incluye `Lineas` en ambos repos. El resume de GRUPO (grupoId != null) re-sincroniza igual que
hoy (ahí el contenido canónico es el del grupo). Re-correr 1.1 (verde) + suite completa.

---

## Etapa 2 — N-3 (P2): reintento de consolidado CM con vouchers nuevos (mail completo)

### 2.1 Test PRIMERO, con contextos separados (debe quedar ROJO)

`CerrarPeriodo_ReintentoConVoucherNuevo_ConContextosSeparados_IncluyeTodosLosVouchers`,
usando el builder de contextos separados CM existente (patrón de
`EmitirMasivo_ConContextosSeparados_CM_YaSinBug`, ServiceFlowTests.cs:477):

1. Sembrar agencia + barco + Configuracion/PV. Crear voucher V1 del período.
2. AFIP FALLA → `CerrarPeriodoAsync(2026, 6)` → consolidado `Pendiente` con V1 vinculado.
3. Crear voucher V2 (misma agencia/período). AFIP OK → `CerrarPeriodoAsync(2026, 6)`.
4. Asserts: el campo de cantidad de vouchers del `ResultadoCierrePorAgencia` == **2**
   (verificar el nombre real de la propiedad); `recibo.Importe == V1 + V2`; el recibo tiene
   2 `Lineas`; y — con un `ICentroMaritimoPdfService` fake propio del test que capture la
   lista pasada a `GenerarPdfDescargaAsync` — el envío recibió **2 vouchers**
   (correr con `enviarMail: true`, mail fake OK).

Hoy: cantidad reportada = 1 y el PDF del mail sale con 1 voucher (colección stale) → rojo.
Con contexto compartido este test pasaría — por eso DEBE ser con contextos separados.

### 2.2 El fix

`CentroMaritimoReciboService.ProcesarCierreAgenciaAsync`, rama `string.IsNullOrEmpty(existente.CAE)`
(~líneas 137-156): después de `MarcarConsolidadosAsync` **recargar el consolidado** y armar
todo desde `existente.Vouchers` (eliminando el `Concat`):

```csharp
if (string.IsNullOrEmpty(existente.CAE))
{
    if (vouchersAgencia.Count > 0)
    {
        await _vouchers.MarcarConsolidadosAsync(vouchersAgencia.Select(v => v.Id), existente.Id, ct);
        // N-3: los vínculos se guardaron en OTRO DbContext (transient). Recargar para que la
        // colección Vouchers de ESTE contexto incluya los nuevos (mail y count correctos).
        existente = await _recibos.GetConsolidadoAsync(agenciaId, anio, mes, ct)
                    ?? throw new InvalidOperationException("El consolidado desapareció durante el reintento.");
    }
    var todosVouchers = existente.Vouchers.OrderBy(v => v.Numero).ToList();
    existente.Importe = todosVouchers.Sum(v => v.Importe);
    existente.Detalle = "Vouchers Nros: " + string.Join(", ", todosVouchers.Select(v => v.Numero));
    existente.Lineas.Clear();
    foreach (var (v, i) in todosVouchers.Select((v, i) => (v, i)))
        existente.Lineas.Add(new ReciboLinea { /* misma proyección que hoy */ });
}
```

Nota: `GetConsolidadoAsync` es tracking en el mismo contexto de `_recibos`, así que devuelve
la MISMA instancia con la colección actualizada por fix-up. `agenciaExistente` (línea 133)
sigue siendo válida. No tocar `ProcesarReciboAsync`. Re-correr 2.1 (verde) + suite.

---

## Etapa 3 — N-4 (P2): logger que registre algo más que excepciones (ambas apps) + mitigación N-2

### 3.1 N-4 · FileLogger

`CamaraPortuaria.UI\Logging\FileLoggerProvider.cs:97` y gemelo CM, método `FileLogger.Log`:

```csharp
// antes:  if (exception is null) return;
if (exception is null && logLevel < LogLevel.Warning) return;
```

Con esto los `LogError`/`LogWarning` sin excepción (rechazos AFIP, vencimiento CAE faltante,
NC autorizada) quedan en el archivo; `Information/Debug` siguen filtrados (logs chicos).
No hay tests de UI: verificación manual en Etapa 9.

### 3.2 Mitigación N-2 · dejar rastro del CAE de la NC ANTES de persistirla (ambas apps)

En ambos `AnularReciboAsync`, entre la construcción de `nota` y `AnularConNotaAsync(...)`:

```csharp
// N-2: si la persistencia local fallara, este log es el único registro del comprobante autorizado.
_logger.LogWarning("NC autorizada por AFIP: PV {Pv} Nro {Numero} CAE {Cae} (recibo {ReciboId}) — persistiendo…",
    nota.PuntoDeVenta, nota.NumeroComprobante, nota.CAE, recibo.Id);
```

(Nivel Warning a propósito: con 3.1 se escribe siempre.) El patrón Pendiente-first para NC
queda **[DIFERIDO]** (requiere estado en `NotaDeCredito` → esquema).

---

## Etapa 4 — P2-2 (P2): reconciliación `FECompConsultar` tras error de comunicación (Afip.Net)

> Diseño completo en `AUDITORIA-2026-06-11.md` § "Diseño propuesto — P2-2". El contrato
> generado YA tiene `FECompConsultarAsync` (`Afip.Net/Soap/Generated/WsfeReference.cs:3703`).
> Alcance: SOLO reconciliación in-flight (dentro del mismo `SolicitarCaeAsync`). El reintento
> "en frío" (app cerrada entre medio) NO se reconcilia — se documenta en Etapa 7.2.

### 4.1 Abstracciones

En `Afip.Net/Abstractions/IWsfeClient.cs` agregar al final:

```csharp
/// <summary>Comprobante ya autorizado, devuelto por FECompConsultar.</summary>
public record WsfeComprobanteConsultado
{
    public required long     Numero              { get; init; }
    public required decimal  ImporteTotal        { get; init; }
    public required long     DocNro              { get; init; }
    public required DateTime FechaComprobante    { get; init; }
    public string?           Cae                 { get; init; }
    public DateTime?         FechaVencimientoCae { get; init; }
}
```

y a la interfaz `IWsfeClient`:

```csharp
/// <summary>FECompConsultar — datos de un comprobante autorizado; null si no existe (error 602).</summary>
Task<WsfeComprobanteConsultado?> ConsultarComprobanteAsync(string token, string sign, string cuit,
    int puntoVenta, int tipoComprobante, long numero, bool usarHomologacion, CancellationToken ct = default);
```

### 4.2 Implementación SOAP

En el cliente SOAP real (`Afip.Net/Soap/WsfeSoapClient.cs` o equivalente — donde están
`DummyAsync`/`SolicitarCaeAsync`): implementar con `FECompConsultarAsync`
(`FeCompConsReq { CbteTipo, PtoVta, CbteNro }` + `FEAuthRequest` como los otros métodos).
Mapear desde `ResultGet`: `CbteDesde`→Numero, `ImpTotal`→ImporteTotal, `DocNro`→DocNro,
`CbteFch` (string `yyyyMMdd`)→FechaComprobante, `CodAutorizacion`→Cae, `FchVto`→vencimiento —
reusar los helpers de parseo de fecha de `WsfeMapper` (respuesta). Si la respuesta trae
`Errors` con código **602** (comprobante inexistente) → devolver `null`; otros errores →
lanzar (que lo capture el caller). Ajustar nombres reales del generado ANTES de aplicar.

### 4.3 Mock

`Afip.Net.Mock/MockWsfeClient.cs`: implementar el método devolviendo `null` (con una
propiedad pública opcional `ComprobanteConsultado` para que los tests puedan setearlo).

### 4.4 WsfeService

`Afip.Net/Wsfe/WsfeService.cs:57`: envolver SOLO la llamada `_wsfe.SolicitarCaeAsync`:

```csharp
WsfeCaeResponse resp;
try
{
    resp = await _wsfe.SolicitarCaeAsync(t.Token, t.Sign, options.Cuit, soap, options.UsarHomologacion, ct);
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    // P2-2: la respuesta pudo perderse DESPUÉS de que AFIP autorizara `numero`.
    var reconciliado = await TryReconciliarAsync(t, options, request, numero, ct);
    if (reconciliado is not null) return reconciliado;
    throw;
}
```

```csharp
private async Task<AfipCaeResult?> TryReconciliarAsync(/* ticket */ t, AfipOptions options,
    AfipComprobanteRequest request, long numero, CancellationToken ct)
{
    try
    {
        var c = await _wsfe.ConsultarComprobanteAsync(t.Token, t.Sign, options.Cuit,
            request.PuntoDeVenta, request.CodigoComprobante, numero, options.UsarHomologacion, ct);
        if (c is null || string.IsNullOrEmpty(c.Cae)) return null;
        var coincide = c.ImporteTotal == request.ImporteTotal
                    && c.DocNro == request.DocNroReceptor
                    && c.FechaComprobante.Date == request.FechaComprobante.Date;
        return coincide
            ? new AfipCaeResult { Aprobado = true, Cae = c.Cae, FechaVencimientoCae = c.FechaVencimientoCae,
                                  Numero = c.Numero, Observaciones = "Reconciliado vía FECompConsultar tras error de comunicación." }
            : null;
    }
    catch { return null; }   // la reconciliación nunca debe enmascarar el error original
}
```

(Tipar `t` con el tipo real que devuelve `_ticket.GetTicketAsync`.) Exponer también
`ConsultarComprobanteAsync` en `IWsfeService`/`WsfeService` como passthrough (para uso futuro
de diagnóstico) — 5 líneas, mismo patrón que `UltimoComprobanteAsync`.

### 4.5 Tests

Donde viven los tests de Afip.Net en `PuertoBB.Tests` (junto a `AfipMappingTests`), con un
mock de `IWsfeClient` + `ITicketProvider` fake:

- `SolicitarCae_TimeoutYConsultaCoincide_DevuelveCaeReconciliado`: `SolicitarCaeAsync` lanza
  `TimeoutException`; `ConsultarComprobanteAsync` devuelve match (mismo importe/doc/fecha) →
  resultado `Aprobado == true`, mismo `Numero`, CAE del consultado.
- `SolicitarCae_TimeoutYConsultaNoCoincide_PropagaError`: consulta devuelve importe distinto
  → se propaga `TimeoutException`.
- `SolicitarCae_TimeoutYConsultaFalla_PropagaErrorOriginal`: consulta lanza → se propaga
  `TimeoutException` (no la de la consulta).

---

## Etapa 5 — P3 baratos

| Ítem | Cambio prescripto |
|---|---|
| 5.1 · N-5 | `Afip.Documentos/Pdf/ComprobanteTemplate.cs:~447`: envolver la línea "Vto. CAE …" en `if (_doc.FechaVencimientoCae > DateTime.MinValue)` (ajustar al bloque real: es un `t.Span` dentro del pie junto al CAE). Smoke test en Afip.Documentos.Tests: doc con `FechaVencimientoCae = default` → PDF válido sin excepción. |
| 5.2 · P3-7 | `Afip.Net/Soap/WsfeMapper.cs:24-29`: todo cast de importe `(double)x` pasa a `(double)Math.Round(x, 2, MidpointRounding.AwayFromZero)`. |
| 5.3 · N-6 | Anclar el invariante "total = suma de líneas": en 2 tests existentes por app (uno masivo, uno individual/consolidado) agregar `Assert.Equal(recibo.Lineas.Sum(l => l.Importe), recibo.Importe);`. NO tocar el template (la parte "magic string RECIBO" de P3-19 queda aceptada). |
| 5.4 · N-7 | Ambos PdfService, en el método de PDF de NC: `var original = nc.ReciboOriginal ?? throw new InvalidOperationException("GenerarPdfNotaDeCredito requiere ReciboOriginal cargado (Include).");` y usar `original` en lugar de los accesos repetidos a la navegación. |
| 5.5 · N-9 | `doc/decisiones/registro-decisiones.md`: agregar **D-22** (5-8 líneas): la grilla de Vouchers ya no muestra columna "Estado" (`VoucherItem.EstadoTexto` eliminado); el estado del ciclo se consulta en Cierre de Período; motivo: el estado por voucher duplicaba el del recibo consolidado y confundía. |
| 5.6 · P2-10 | Ídem: agregar **D-23**: los datos reales del SeedData se mantienen (repo privado; el seed solo corre con `ModoDemo=true`). Cierra P2-10 del informe 06-10. |
| 5.7 · N-12 | `CentroMaritimo.UI/Views/VouchersPage.xaml`: renumerar los `Grid.Row` para eliminar el hueco que dejó la columna/fila removida (cosmético; si el layout real no muestra hueco, SALTAR). |
| 5.8 · P3-15 parcial **[OPCIONAL]** | `CamaraPortuaria.UI/Services/DialogService.cs:45-54`: nombrar los PDFs temporales con prefijo propio (`puertobb_cp_*.pdf`) y, al construir el servicio, borrar los de más de 1 día (`try/catch` por archivo). El visor embebido NO se porta (sigue diferido). |

---

## Etapa 6 — Red de tests (N-10, N-11)

Todos en el harness existente; nombres ilustrativos, seguir la convención de la suite:

1. **ReenviarMail:** (CP) `ReenviarMail_ReciboEmitido_EnviaYPasaAEnviado`; (CM) ídem +
   `ReenviarMail_Consolidado_UsaPdfDescargaConTodosLosVouchers` (fake PDF capturador, como 2.1).
2. **MarcarPagado:** (CP) happy path `Enviado → Pagado` con `FechaPago` seteada; (CP y CM)
   `MarcarPagado_ReciboPendiente_Falla`.
3. **EmitirDeGrupoAsync CM:** happy path (1 agencia del grupo → recibo con líneas del grupo y
   `EmisionGrupo` creado).
4. **Índice de numeración AFIP:** test de repositorio (CP o CM): dos recibos con el mismo
   `(PuntoDeVenta, NumeroComprobante>0, CodigoAfip)` → el segundo guarda con `ReciboException`
   /`DbUpdateException`; dos con `NumeroComprobante = 0` → permitidos (filtro del índice).
5. **Anulación con AFIP fallando (CP y CM):** AFIP fake en FALLA → `AnularReciboAsync` devuelve
   Fail, el recibo NO queda `Anulado` y no existe NC persistida.
6. **Contextos separados extra (CM):** `AnularConsolidado_ConContextosSeparados_DesvinculaVouchers`
   y `EmitirIndividual_ConContextosSeparados_CM` (espejo del de CP). El reintento de
   consolidado ya quedó cubierto en 2.1.
7. **N-11 · QR:** en `AfipQrPayloadTests`: importes `10_000_000.00m` y `1234.56m` → el JSON
   decodificado del QR no contiene `E`/`e` en el campo importe y usa punto decimal.
8. **N-11 · PDF multipágina:** en Afip.Documentos.Tests: comprobante con ≥15 ítems → PDF
   válido y con ≥2 páginas (contarlas con PdfSharp `PdfReader.Open(..., InformationOnly)` —
   ya es dependencia vía `PdfMerger`; si Afip.Documentos.Tests no referencia PdfSharp,
   agregar la referencia de test o mover el test a `PuertoBB.Tests/VoucherPdfTests`).

---

## Etapa 7 — Entregabilidad (ítems 2, 9 y 10 del checklist; SIN distribución)

### 7.1 Versión visible (ítem 10)

1. En ambos csproj de UI: `<Version>1.0.0</Version>` dentro del primer `<PropertyGroup>`.
2. En ambos `MainWindow.xaml.cs` (donde hoy se anexa el sufijo MODO DEMO, línea ~20):

```csharp
var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
Title += $" · v{ver?.ToString(3)}";
if (App.ModoDemo) Title += " — MODO DEMO";
```

(El orden final del título: `… · v1.0.0 — MODO DEMO`.)

### 7.2 `doc/usuario/paso-a-produccion.md` (ítem 2)

Escribirlo con este contenido (estilo de `afip-configuracion.md`):

1. **Qué conmuta el modo:** `appsettings.json` junto al exe →
   `{ "PuertoBB": { "ModoDemo": false, "Afip": "Real" } }`; qué cambia (sin seed, mail real,
   AFIP real, desaparece el sufijo "MODO DEMO" del título).
2. **Checklist de primera corrida** (base vacía), en orden: Configuración → Datos del emisor
   (razón social, CUIT, IIBB, inicio de actividades) → Punto de venta + certificado (link a
   `afip-configuracion.md`) → **Probar conexión** → Correo (SMTP + remitente) → alta de
   entidades/grupos (o restaurar un backup).
3. **Dónde viven los datos:** `%LocalAppData%\PuertoBB\<App>` (base, logs, ticket cache) —
   referenciar `base-de-datos.md`; recomendación de backup (frecuencia mensual mínimo, tras
   cada cierre de período en CM).
4. **Limitación conocida (P2-2):** si la app se cierra justo durante una emisión con error de
   red, verificar en AFIP ("Comprobantes en línea") si el comprobante salió antes de
   reintentar; la reconciliación automática cubre solo el reintento inmediato.

### 7.3 Manuales operativos (ítem 9)

Crear `doc/usuario/manual-camara-portuaria.md` y `doc/usuario/manual-centro-maritimo.md`.
Fuente de verdad: `doc/negocio/*.md` + las Views/ViewModels reales (verificar cada acción
contra la UI antes de documentarla — no inventar pantallas). Estructura por manual:

- Pantalla por pantalla (sidebar): Inicio/Control de pagos, Recibos, Emisión masiva,
  Empresas|Agencias (+ emails), Grupos (+ ítems multi-línea), [CM: Vouchers, Barcos, Cierre
  de período], Configuración.
- Flujos con pasos numerados: emisión masiva (período+grupo → Emitir / Enviar / Emitir y
  enviar; qué significa cada estado y color), emisión individual, reintento de un Pendiente,
  anulación con NC (checkbox de mail), reenvío, marcar pagado, [CM: alta de voucher, cierre
  de período, anular consolidado y reemitir, cobro extraordinario en mes cerrado].
- Backup y restauración (referenciar `base-de-datos.md`).
- Tabla de estados (copiar la de `funcionalidad-compartida.md` con colores).

---

## Etapa 8 — Docs técnicos (N-8) + cierre de informes

1. `doc/negocio/cierre-periodo.md`: marcar Iteraciones 2 y 3 como hechas (las acciones
   "Generar recibo" y "Cerrar período" están operativas); reemplazar la nota "Anulado se trata
   transitoriamente como Pendiente… caso borde a refinar" por el comportamiento real (anular
   desvincula los vouchers y el período se puede reemitir — P1-3); quitar los "(Próxima
   iteración)" de la tabla de acciones.
2. `doc/arquitectura/convenciones.md` (~líneas 80-90): reemplazar el snippet Serilog por el
   real: `FileLoggerProvider` propio (archivo diario `app-yyyyMMdd.log` en
   `%LocalAppData%\PuertoBB\<App>\Logs`, retención 30 archivos, escribe Warning+ y toda
   entrada con excepción).
3. `doc/decisiones/estado-implementacion.md`: corregir la mención "Serilog" por el provider
   propio.
4. `doc/negocio/funcionalidad-compartida.md:56`: precisar: "se calcula visualmente cuando
   `FechaVencimientoPago < hoy` y el estado es **Emitido o Enviado**".
5. `prompt_inicializacion.md` y `doc/mejoras/informe-auditoria.md`: agregar al inicio
   `> **Documento histórico** (anterior a D-21): el "apoderado fiscal" descripto acá fue
   eliminado del producto.` — NO reescribirlos.
6. `AUDITORIA-2026-06-11.md`: marcar cada hallazgo ✅/⏭️ y actualizar la tabla del checklist
   de entrega (ítems 2, 9, 10 → ✅; ítem 1 → "fuera de alcance por decisión del usuario").
7. Completar `fixing-log-2026-06-11.md` con el estado final y los SALTADOS para que el modelo
   principal los retome.

---

## Etapa 9 — Verificación final

1. Cerrar apps/VS → `dotnet build PuertoBB.slnx` (0 warnings) + suite completa
   (esperable: ~115-120 tests verdes; anotar el número real).
2. `/validar-plataforma`.
3. Smoke manual en modo demo (borrar `.db` viejas de `%LocalAppData%\PuertoBB` primero):
   - **N-1 en vivo (CP):** emitir dos individuales con contenido distinto el mismo período →
     2 recibos.
   - **N-3 en vivo (CM):** no es simulable sin fallo de AFIP en demo — cubierto por el test
     de 2.1; verificar igual el cierre de período normal + preview del PDF único.
   - **N-4 en vivo:** poner `"Afip": "Real"` sin certificado, intentar emitir → la emisión
     falla con mensaje claro Y queda una línea en `%LocalAppData%\…\Logs\app-*.log`. Volver
     a `"Mock"`.
   - Título de ventana muestra `v1.0.0` (+ "MODO DEMO" en demo).
   - Backup + restaurar + reabrir.
4. Releer los tres docs nuevos de `doc/usuario/` contra la UI real (nombres de botones y
   pestañas exactos).

## Diferidos que QUEDAN tras este plan (no tocar)

- **N-2 completo** (Pendiente-first para NC): requiere estado/esquema en `NotaDeCredito`.
- **P3-15** (visor PDF embebido CM→CP): solo entró la limpieza de temporales (5.8).
- **Distribución/instalador** (checklist ítem 1): excluido por decisión del usuario.
- **Reconciliación "en frío"** (P2-2 extendido): documentada como limitación en 7.2.
- P3 aceptados del informe 06-10: P3-4, P3-5, P3-6, P3-13, P3-14, P3-17, P3-19 (parte
  template), P3-21, P3-22.
- Pruebas externas (checklist 4 y 5): AFIP homologación con `.p12` real y SMTP real — las
  hace el usuario siguiendo `afip-configuracion.md` + `paso-a-produccion.md`.
