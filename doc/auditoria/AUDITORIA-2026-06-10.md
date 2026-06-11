# Auditoría Extensiva PuertoBB — 2026-06-10

Ejecutada según `plan-auditoria-2026-06-10.md` (14 fases; fases mecánicas con subagentes,
fases de negocio/AFIP/seguridad/modelo/flujos con el modelo principal; todo hallazgo P0/P1
verificado leyendo el código). **No se modificó código.** Auditoría sobre el working tree
(incluye cambios sin commitear al 2026-06-10).

## Línea base

- `dotnet build PuertoBB.slnx` → 0 errores, 0 warnings.
- `dotnet test` → **74/74 verdes** (16 Afip.Documentos.Tests + 58 PuertoBB.Tests).
- `dotnet list package --vulnerable` → sin vulnerabilidades en los 10 proyectos.
- `dotnet ef migrations has-pending-model-changes` → ambos contextos sincronizados (una migración `Inicial` por contexto).

⚠️ **Los tests verdes no garantizan el runtime real**: `ServiceFlowTests` construye todos los
repositorios alrededor de **un único DbContext compartido** (`SqliteTestDb` + `BuildService`),
mientras que las apps registran el DbContext como **Transient** (un contexto por repositorio,
`PuertoBB.Infrastructure/DependencyInjection.cs:18,32`). Eso enmascara el P0-1.

## Resumen

| Prioridad | Cantidad | Descripción |
|---|---|---|
| **P0** | 1 | Rompe la emisión de recibos en CP en runtime |
| **P1** | 9 | Bugs funcionales / riesgo fiscal u operativo real |
| **P2** | 14 | Robustez / deuda importante |
| **P3** | ~22 | Mejoras menores |

---

## P0 — Crítico

### P0-1 ✅ · CP: toda emisión nueva falla en runtime — navegación `Empresa` asignada con entidad de otro DbContext

**Archivo:** `PuertoBB.Services/Negocio/CamaraPortuariaReciboService.cs:171` (`Empresa = empresa` dentro de `EmitirOResumirAsync`).

**Problema:** El DbContext es **Transient** y cada repositorio recibe su propia instancia
(`Infrastructure/DependencyInjection.cs:18`). La `empresa` viene del contexto de `_grupos`
(`GetConMiembrosAsync`) o `_empresas` (`GetConDetalleAsync`); al asignarla como navegación del
`Recibo` nuevo y llamar `_recibos.AddAsync(recibo)`, EF marca **todo el grafo alcanzable como
`Added`** (la Empresa, sus Emails, sus EmpresaGrupo…) e intenta re-INSERTarlos con sus Id
existentes → violación de PK (`UNIQUE Empresas.Id`) → `DbUpdateException` → `ReciboException`
→ cada emisión devuelve "No se pudo guardar Recibo…".

**Evidencia de que es exactamente este bug:** CM lo sufrió y lo arregló — `ConstruirRecibo`
(CentroMaritimoReciboService.cs:392-394) NO asigna la navegación y el comentario lo explica:
*"la agencia viene de OTRO contexto y EF intentaría reinsertarla al guardar (UNIQUE
Agencias.Id)"*. El fix nunca llegó a CP. Los tests no lo detectan porque comparten un solo
contexto (ver Línea base).

**Impacto:** Emisión masiva, individual y de grupo de Cámara Portuaria **no funcionan** para
recibos nuevos (el resume de un recibo Pendiente existente sí funciona, porque ahí la empresa
viene del propio contexto de `_recibos`).

**Fix:** Replicar el patrón de CM: quitar `Empresa = empresa` (dejar solo `EmpresaId`);
`empresa` ya llega con Emails cargados para el mail, y el PDF usa el snapshot `Receptor*` y
`Lineas` (no necesita la navegación). **Agregar un test de regresión que use repositorios con
DbContexts separados** (es la brecha que ocultó esto).

---

## P1 — Bugs funcionales / riesgo real

### P1-1 ✅ · CM cierre de período: el CAE se obtiene ANTES de persistir el recibo + dos SaveChanges sin transacción

**Archivo:** `CentroMaritimoReciboService.cs:146-152` (`ProcesarCierreAgenciaAsync`).

1. `EmitirCaeAsync` → `AplicarCae` → recién después `_recibos.AddAsync(recibo)`. Si el
   `AddAsync` falla (constraint, disco, crash), **el comprobante ya fue autorizado por AFIP
   pero no queda registrado localmente**: hueco en la numeración + comprobante huérfano en AFIP
   que la app desconoce. El flujo de cuotas hace lo correcto (persistir `Pendiente` primero,
   CAE después, idempotente); el consolidado quedó con el patrón viejo.
2. `AddAsync` y `MarcarConsolidadosAsync` son dos `SaveChanges` en contextos distintos. Si el
   segundo falla: recibo consolidado con CAE persistido + vouchers aún "pendientes". Al
   reintentar, `ExisteConsolidadoAsync` devuelve "Ya existe" → los vouchers quedan **atrapados
   para siempre** (sin vínculo y sin forma de consolidarlos por UI).

**Fix:** llevar el consolidado al patrón Pendiente-first de las cuotas, y persistir
recibo+marcado de vouchers en una sola operación (mismo DbContext / repositorio con método
`AddConVouchersAsync`, o transacción explícita).

### P1-2 ✅ · Anulación: la NC con CAE se persiste al final, sin transacción (ambas apps)

**Archivos:** `CamaraPortuariaReciboService.cs:336-355`, `CentroMaritimoReciboService.cs:526-545`.

Orden actual: pedir CAE de la NC a AFIP → `UpdateAsync(recibo→Anulado)` (SaveChanges #1) →
`_notas.AddAsync(nota)` (SaveChanges #2). Si el `AddAsync` falla, la NC **ya existe
fiscalmente en AFIP** pero su número/CAE no quedan registrados en ningún lado (peor que la
inconsistencia recibo-Anulado-sin-NC: es pérdida de un comprobante fiscal). Fix: persistir la
NC primero (o todo en una transacción); idealmente el mismo patrón Pendiente-first que los
recibos.

### P1-3 ✅ · CM: reemitir un período tras anular el consolidado es imposible (la UI dice lo contrario)

`cierre-periodo.md` documenta que un consolidado `Anulado` se muestra como **Pendiente** "para
permitir reemisión" (`VoucherService.MapEstado`, VoucherService.cs:140). Pero la reemisión está
bloqueada por tres lados:

1. `AnularReciboAsync` **no desvincula los vouchers** (`Voucher.ReciboId` queda apuntando al
   recibo anulado) → `GetPendientesByPeriodoAsync` (ReciboId IS NULL) no los devuelve.
2. `ExisteConsolidadoAsync` (`CentroMaritimo/ReciboRepository.cs:50-55`) no excluye `Anulado`
   → "Ya existe un recibo consolidado para este período".
3. El índice único parcial (`CentroMaritimo/ReciboConfiguration.cs:30-32`,
   `WHERE EsConsolidadoVouchers = 1`) tampoco excluye anulados → aunque se arreglen (1) y (2),
   el INSERT del nuevo consolidado violaría el índice.

**Fix coordinado:** al anular un consolidado poner `ReciboId = null` en sus vouchers; excluir
`Estado != Anulado` en `ExisteConsolidadoAsync`; extender el filtro del índice
(`AND "Estado" <> 'Anulado'`) con su migración.

### P1-4 ✅ · Clave de emisión individual sobre-restrictiva: bloquea cobros extraordinarios legítimos

**Archivos:** `FiltrarPorClave` en ambos `ReciboRepository` (CP:34-40, CM:36-42) + `EmitirOResumirAsync`.

`GetPorClaveAsync(entidadId, grupoId: null, anio, mes)` matchea **cualquier** recibo sin
`EmisionGrupo` de esa entidad y período. Consecuencias:

- **Ambas apps:** no se pueden emitir dos recibos individuales a la misma entidad en el mismo
  período (el segundo devuelve "Ya existe un recibo para este período"). El negocio requiere
  cobros extraordinarios independientes (`funcionalidad-compartida.md`).
- **CM (peor):** el recibo consolidado de vouchers también tiene `EmisionGrupo == null`, así
  que en un mes con cierre ya hecho **no se puede emitir ningún recibo individual** a esa
  agencia.

**Fix:** distinguir el "slot" de emisión: excluir consolidados del filtro
(`!r.EsConsolidadoVouchers` en CM) y repensar la clave de individuales (p.ej. permitir N
individuales por período y validar duplicados solo dentro de la misma operación de reintento,
o un identificador de emisión explícito).

### P1-5 ✅ · Restaurar backup casi seguro falla: pooling de SQLite mantiene el archivo abierto

**Archivos:** `CamaraPortuaria.UI/Services/BackupService.cs:45-62` y homólogo CM.

`RestaurarAsync` cierra solo la conexión de **su** DbContext y hace `File.Copy(origen, dbPath,
overwrite: true)`. `Microsoft.Data.Sqlite` poolea conexiones por default: las conexiones de
todos los DbContext transient previos (y las del pool ya "cerradas") mantienen un handle sobre
el archivo → `IOException` (archivo en uso). **Fix:** `SqliteConnection.ClearAllPools()` antes
del copy (y considerar reiniciar la app después de restaurar para no operar con trackers
viejos). Verificar manualmente: es muy probable que hoy la restauración nunca funcione.

### P1-6 ✅ · Arranque: si `MigrateAsync` falla, la app queda como proceso zombi sin ventana (ambas apps)

**Archivos:** `CamaraPortuaria.UI/App.xaml.cs:55-80`, `CentroMaritimo.UI/App.xaml.cs:55-80`.

`OnStartup` es `async void` sin try/catch. Una excepción en `InicializarBaseDeDatosAsync` (DB
corrupta, AppData sin permisos) llega a `DispatcherUnhandledException`, que marca
`e.Handled = true` y muestra el error vía `MostrarErrorCritico` (en ese momento el
DialogService no está inicializado → cae al `MessageBox`). Resultado: `main.Show()` nunca corre
y el proceso queda vivo, invisible, sin ventanas. **Fix:** try/catch en `OnStartup` con
`MessageBox` + `Shutdown(1)`.

### P1-7 ✅ · PDF de Nota de Crédito no imprime el comprobante asociado (normativa AFIP)

**Archivo:** `Afip.Documentos/Pdf/ComprobanteTemplate.cs` (el campo
`ComprobanteDocumento.ComprobanteAsociado` se popula en ambos PdfService pero el template no lo
referencia en ninguna parte). La normativa de facturación electrónica exige que la NC impresa
identifique el comprobante que anula (tipo, PV, número). **Fix:** banda condicional en el
template ("Comprobante asociado: RECIBO C — 0001-00000123").

### P1-8 ✅ · Comprobante impreso sin Ingresos Brutos ni Inicio de Actividades del emisor (normativa AFIP)

**Archivos:** `PuertoBB.Services/Pdf/CamaraPortuariaPdfService.cs` (BuildComprobante inline) y
`CentroMaritimoPdfService.BuildEmisor`. El template ya soporta `EmisorDocumento.IngresosBrutos`
e `InicioActividades` (los renderiza condicionalmente) pero **nadie los popula** porque no
existen en `Configuracion`. **Fix:** agregar ambos campos a `Configuracion` (+ migración + UI
de Configuración en ambas apps) y poblarlos en los dos servicios PDF.

### P1-9 ✅ · `ModoDemo` y `AfipModo` son `const` en el código fuente — riesgo de release inválido

**Archivos:** `CamaraPortuaria.UI/App.xaml.cs:44,50` y homólogo CM.

Hoy `ModoDemo = true` + `Afip = AfipModo.Mock`. Pasar a producción exige editar el fuente y
recompilar; no hay configuración externa. Un build distribuido sin el cambio emite comprobantes
mock y "manda" mails falsos sin que nada lo delate a simple vista. **Fix:** mover a
configuración externa (appsettings/variable de entorno) o, mínimo, `#warning`/`#error` si se
compila Release con `ModoDemo = true`, y un cartel visible de "MODO DEMO" en la UI.

---

## P2 — Robustez / deuda importante

| ID | Archivo(s) | Hallazgo y fix sugerido |
|---|---|---|
| P2-1 ✅ | `EmisionMasivaViewModel.cs:40-56` (ambas) | Los setters de Mes/Año/Grupo disparan `_ = CargarEstadoAsync()` sin cancelar la carga anterior: cargas superpuestas pueden mezclar `Items` de dos períodos. Fix: `CancellationTokenSource` por carga (cancelar la previa). |
| P2-2 ⏭️ | `Afip.Net/Wsfe/WsfeService.cs:32-67` | Si la respuesta de `FECAESolicitar` se pierde (timeout de red post-aprobación), AFIP queda con un comprobante autorizado que la app ignora; el reintento emite otro número. Fix: al reintentar, consultar `FECompConsultar` del último número para reconciliar antes de pedir CAE nuevo. |
| P2-3 ✅ | `PuertoBB.Services/Afip/AfipService.cs:54` | Si AFIP no devuelve vencimiento de CAE se **fabrica** `FechaEmision.AddDays(10)` y se persiste como real. Fix: persistir null/avisar, no inventar un dato fiscal. |
| P2-4 ✅ | `Afip.Net/Wsaa/TraBuilder.cs:35-44` | El `X509Certificate2` nunca se dispone y `PersistKeySet|MachineKeySet` acumula contenedores de clave en `C:\ProgramData\...\MachineKeys` en cada login WSAA. Fix: `using var cert = ...` + `EphemeralKeySet`. (El comentario "SHA1+RSA" también está desactualizado: CmsSigner usa SHA256.) |
| P2-5 ✅ | `ReciboConfiguration.cs` (ambas) | No hay índice único de numeración AFIP `(PuntoDeVenta, NumeroComprobante, CodigoAfip)`. La única guardia es lógica de servicio. Fix: índice único con filtro `NumeroComprobante > 0` (+ migración). |
| P2-6 ✅ | `ControlPagosViewModel.cs:21` (ambas) | El checkbox **"Solo vencidos" no recarga la lista** (a diferencia de `IncluirMorosos`, línea 24-27). Fix: `if (SetField(...)) _ = BuscarAsync();`. |
| P2-7 ✅ | `CentroMaritimo.UI/Views/VouchersPage.xaml:129` | Importe del voucher en `TextBox` contra `decimal`: texto inválido = binding falla silencioso y se guarda el importe anterior. Fix: `ui:NumberBox` (ya usado en Grupos) o converter con validación. |
| P2-8 ✅ | `EmpresasViewModel.cs:155` / `AgenciasViewModel` | CUIT solo se valida como "no vacío". AFIP lo rechaza recién al emitir. Fix: validar 11 dígitos + dígito verificador en el ABM. |
| P2-9 ✅ | `BackupService.BackupAsync` (ambas) | `VACUUM INTO` falla si el archivo destino existe (y el SaveFileDialog permite elegir uno existente). Fix: `File.Delete(destino)` previo si existe. |
| P2-10 ⏭️ | `SeedData.cs` (ambas, CP:41-68) | **Datos reales** (CUITs y emails corporativos de ~29 empresas y 13 agencias) commiteados en git como datos "demo". Fix: mover a un archivo externo no commiteado o anonimizar el seed; purgar del historial si el repo se comparte. |
| P2-11 ✅ | `MailConfigProvider` (ambas) + `Configuracion` | `SmtpPassword` en texto plano (decisión documentada), pero la infraestructura `ISecretProtector` ya existe y se usa para el cert. Fix barato: `Protect` al guardar + `Unprotect` en el provider. |
| P2-12 ✅ | `doc/arquitectura/datos.md`, `flujos.md`, `funcionalidad-compartida.md` | Documentación desactualizada respecto al modelo real: falta `ReciboLinea`/`Lineas`, snapshot `Receptor*`, `EmisionGrupo` (el doc aún muestra `GrupoFacturacionId` en Recibo), estado `Pendiente`, `UltimoErrorCae/Mail`, `EsMoroso`, `PuntoDeVenta` como entidad, `ImporteVoucherPredeterminado`. Fix: actualizar los tres docs. |
| P2-13 ✅ | `ReenviarMailAsync` (CP:376, CM:563) | Sin guard de CAE: un recibo `Pendiente` mandaría un PDF sin CAE/QR. Hoy la UI lo impide (`ReciboItem.EsReenviable`), pero el servicio debe defenderse solo. Fix: `if (string.IsNullOrEmpty(recibo.CAE)) return Fail(...)`. |
| P2-14 ✅ | `config.PuntoDeVentaActivo?.Numero ?? 0` (CP:186,320 · CM:416,510) | Sin punto de venta activo se emite/anula con PV=0 y el error aparece recién como rechazo AFIP críptico. Fix: validar y fallar con "Configure un punto de venta activo". |

---

## P3 — Mejoras menores

| ID | Archivo(s) | Hallazgo |
|---|---|---|
| P3-1 ✅ | `CamaraPortuariaReciboService.cs:53-57` + CM:183-186 | N+1 en `GetDuplicadosAsync` (un `AnyAsync` por miembro). Reemplazar por una query. |
| P3-2 ✅ | `CamaraPortuariaReciboService.cs:100-107` + CM:229-237 | N+1 en `EnviarMasivoAsync`; ya existe `GetPorGrupoYPeriodoAsync` (ojo: habría que sumarle `Include(Lineas/Emails)` para este uso). |
| P3-3 ✅ | `App.xaml.cs:121-127` (ambas) | DbContext de startup resuelto del root provider sin scope (no se dispone). Usar `CreateAsyncScope()`. |
| P3-4 | `RepositoryBase.cs:27` | `GetByIdAsync` trackea siempre; para validaciones de solo lectura conviene variante `AsNoTracking`. |
| P3-5 | `ReciboRepository.GetPorGrupoYPeriodoAsync/GetPorPeriodoAsync` | No incluyen `Lineas`; documentarlo para evitar consumidores futuros con datos incompletos. |
| P3-6 | Configuraciones EF | `decimal` como TEXT en SQLite: correcto para precisión, pero `SUM/ORDER BY` en SQL nativo serían lexicográficos. Documentar la restricción. |
| P3-7 | `Afip.Net/Soap/WsfeMapper.cs:24-29` | `(double)req.ImporteTotal` sin `Math.Round(...,2)` previo: riesgo teórico de artefactos binarios en importes. Redondear antes del cast. |
| P3-8 ✅ | `App.xaml.cs OnExit` (ambas) | `async void` sin try/catch; envolver y loguear. |
| P3-9 ✅ | `RecibosViewModel.cs:165/163` | `_ = GuardarConceptosAsync(...)` fire-and-forget; await directo (es rápido y ya tiene catch interno). |
| P3-10 ✅ | `RecibosViewModel`/`ControlPagosViewModel` | `Clear()` + `Add` ítem por ítem en cada filtro (N eventos CollectionChanged). Reemplazo de colección o bulk-update. |
| P3-11 ✅ | `RecibosPage.xaml`/`ControlPagosPage.xaml` (ambas) | Agregar `VirtualizingPanel.VirtualizationMode="Recycling"` a los DataGrid con template columns. |
| P3-12 ✅ | `ConfiguracionPage.xaml.cs:33` (ambas) | Suscripción a `PropertyChanged` sin desuscribir en `Unloaded`. |
| P3-13 | `EmpresasViewModel`/`GruposViewModel`/`ConceptosReciboViewModel` | Propiedades de edición sin notificación propia (patrón `NotificarEdicion()` manual) y `TotalEdit` notificado a mano (`RefrescarTotal`). Migrar a `SetField`/suscripción a `CollectionChanged`. |
| P3-14 | `CierrePeriodoViewModel.cs:172-178` | Envío de mails secuencial sin progreso parcial (13 agencias ≈ 30-40 s de overlay). Progreso "n/N" o concurrencia limitada. |
| P3-15 ⏭️ | `CamaraPortuaria.UI/Services/DialogService.cs:45-54` | PDFs temporales nunca se limpian + divergencia con CM (visor embebido `PdfPreviewDialog`). Portar el visor de CM a CP y limpiar temp. |
| P3-16 ✅ | `GruposViewModel`/`AgenciasViewModel`/`ControlPagosViewModel` (CM) | Mensajes de error/éxito recortados respecto a CP (p.ej. CM perdió el hint "¿tiene recibos asociados?"). Unificar con los textos más descriptivos. |
| P3-17 | `ContadorVoucherRepository.ObtenerSiguienteNumeroAsync` | Doble click rápido = dos contextos leen el mismo `UltimoNumero` (el índice único de Voucher.Numero lo convierte en error visible) y un fallo post-incremento deja hueco. Aceptable, documentar. |
| P3-18 ✅ | `ComprobanteTemplate.cs:277/370` | Recibo sin ítems: importe duplicado (cuerpo con `$`, pie sin `$`). Unificar presentación. |
| P3-19 | `ComprobanteTemplate.cs:266` + `:280` + `Totales` | Detección "es recibo" por magic string `NombreOverride == "RECIBO"`; cuerpo vacío si no hay líneas ni detalle; el total impreso confía en `ImporteTotal` del caller sin validar contra `Sum(Items)`. Endurecer las tres. |
| P3-20 | `Afip.Documentos/Qr/AfipQrPayload.cs` | Test faltante para importes extremos (notación científica en JSON del QR) y footer repetido en multipágina (decisión a documentar). |
| P3-21 | `VouchersViewModel.LimpiarFormulario` | `ImporteNuevo` no se resetea tras crear (¿intencional por `ImporteVoucherPredeterminado`? documentar). |
| P3-22 | Convenciones (fase 13) | Converters+helpers múltiples por archivo; handlers de fila en code-behind de `RecibosPage` (workaround documentable); archivos >600 líneas: `ConfiguracionViewModel` (CP 653 / CM 756), `CentroMaritimoReciboService` (601), `ServiceFlowTests` (658). |

---

## Falsos positivos descartados (no corregir)

1. **Catálogo AFIP código 9** ("debería ser Nota de Débito B"): falso — en la tabla AFIP el 9
   **es Recibo B** (ND B es 7). `TipoComprobanteAfip.Nombre` está correcto; coincide con `datos.md`.
2. **`FchServDesde/Hasta` con formato incorrecto en `WsfeMapper`**: falso — `ServicioDesde/Hasta`
   son `int` con valor `yyyyMMdd` (`PeriodoHelper.PrimerDia/UltimoDia`); su `ToString` produce
   exactamente "20260601".
3. **"Las excepciones de `async void` no llegan a `DispatcherUnhandledException`"** (fase 2):
   impreciso — en WPF sí llegan (se postean al SynchronizationContext). El efecto reportado en
   P1-6 (proceso zombi) es real de todos modos.

## Revisado y OK (no re-mirar)

- **AFIP/WSAA:** cache de ticket por (CUIT, servicio) con double-check lock y margen de 10 min;
  ticket persistido cifrado DPAPI (`FileTicketStore`) con fallback limpio; TRA con uniqueId
  monótono; regla "tipo C sin array IVA" respetada; validaciones de CUIT en el adaptador;
  `ProbarConexionAsync` con diagnóstico útil. `DpapiSecretProtector` correcto (prefijo `dpapi:`,
  migración suave de texto plano).
- **PDF:** todos los caminos generan desde `Lineas`; total = suma de líneas (con tests); QR AFIP
  completo y testeado campo por campo; CAE/vencimiento/tipo/letra/PV-número con padding
  0001-00000008/CUITs/condición IVA/fecha presentes; QR con fallo tolerado sin romper el PDF.
- **EF:** sin lazy loading; `AsNoTracking` en listas; `DeleteBehavior` correcto en todas las
  relaciones (Restrict para entidades con recibos/vouchers; Cascade solo Lineas y EmisionesGrupo);
  índices únicos de `EmisionesGrupo` y `Voucher.Numero`; `DbUpdateException` envuelta con mensaje
  legible; `ConfiguracionRepository.GetAsync` incluye `PuntosDeVenta`.
- **Async/UI:** sin `.Result`/`.Wait()`; sin `Task.Run` tocando UI; emisión masiva secuencial
  (sin DbContext compartido entre tareas); `AsyncRelayCommand` con anti-doble-click y `finally`;
  handlers globales registrados; `IsBusy` siempre liberado; `SnackbarHost` con guard de Dispatcher;
  converters contemplan todos los estados actuales (incluido `Pendiente` y "Moroso"/"No emitido").
- **Seed:** guard `AnyAsync()` impide sembrar sobre una base con datos; el seed solo corre con
  `ModoDemo`.
- **Convenciones (fase 13):** 14 de 17 reglas verificadas OK; receta end-to-end cumplida por
  todas las entidades (PuntoDeVenta como agregado de Configuracion, justificado).
- **Paridad CP↔CM:** fuera de lo reportado (P0-1, P3-15, P3-16), las divergencias son propias
  del dominio CM (vouchers, consolidados, apoderado) o cosméticas; pares restantes idénticos
  salvo renombre (lista completa verificada).

## Orden de corrección sugerido

1. **P0-1** (emisión CP rota) + test de regresión con DbContexts separados.
2. **P1-1, P1-2** (orden CAE/persistencia + transaccionalidad) — comparten el mismo refactor de
   patrón Pendiente-first/contexto compartido; conviene hacerlos juntos.
3. **P1-3, P1-4** (anulación de consolidado + clave de emisión individual) — tocan el mismo
   repositorio/índice; incluyen migración (recordar: una migración por release, squash).
4. **P1-5 a P1-9** (backup, arranque, normativa PDF ×2, ModoDemo).
5. P2 en bloque (varios son one-liners: P2-6, P2-9, P2-13, P2-14).
6. P3 oportunísticamente.

> Nota de verificación: tras los fixes correr `/validar-plataforma` y probar a mano
> (a) emisión masiva CP en modo demo, (b) cierre de período CM + anular + reemitir,
> (c) backup y restauración con la app recién abierta.

---

## Estado de corrección — 2026-06-10

Plan ejecutado vía `plan-fixing-2026-06-10.md`. Suite final: **93/93 tests verdes** (17 Afip.Documentos.Tests + 76 PuertoBB.Tests). Log detallado en `fixing-log.md`.

| Prioridad | Corregidos | Diferidos | Pendientes |
|---|---|---|---|
| P0 | 1/1 | 0 | 0 |
| P1 | 9/9 | 0 | 0 |
| P2 | 12/14 | 2 (P2-2, P2-10) | 0 |
| P3 | 8/22 | 1 (P3-15) | 13 no asignados en plan |

**Ítems diferidos pendientes de retomar:**
- **P2-2** — Reconciliación `FECompConsultar` tras timeout AFIP: requiere diseño en `Afip.Net`.
- **P2-10** — Datos reales en SeedData: decisión del usuario (anonimizar / externalizar / purgar historial).
- **P3-15** — Portar visor PDF embebido de CM a CP + limpiar temporales.
- **P3-4, P3-5, P3-6, P3-7, P3-13, P3-14, P3-17, P3-19, P3-20, P3-21, P3-22** — Mejoras menores no asignadas en el plan.
