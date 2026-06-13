# Auditoría Ponderada PuertoBB — 2026-06-11

Ejecutada según `plan-auditoria-2026-06-11.md` (12 fases; fases mecánicas con subagentes,
fases de negocio/AFIP/modelo/diferidos/entregabilidad con el modelo principal; todo hallazgo
P0/P1/P2 verificado leyendo el código). **No se modificó código.** Auditoría sobre el working
tree (incluye los cambios sin commitear de D-21 al 2026-06-11).

## Línea base

- `dotnet build PuertoBB.slnx -c Release` → **0 errores, 0 warnings** (toda la solución).
  ⚠️ El build **Debug** falla si la app está abierta: Visual Studio + `CentroMaritimo.UI`
  en ejecución bloquean las DLLs (`MSB3027`). No es un problema de código; cerrar la app
  antes de compilar.
- `dotnet test` → **98/98 verdes** (81 PuertoBB.Tests + 17 Afip.Documentos.Tests).
- `dotnet ef migrations has-pending-model-changes` → ambos contextos sincronizados; una sola
  migración `Inicial` por contexto (CM regenerada `20260611210050_Inicial`).
- `dotnet list package --vulnerable` → sin vulnerabilidades en los 10 proyectos.
- ⚠️ Durante la planificación de esta auditoría, 5 archivos con cambios reales (`SeedData`,
  `VouchersViewModel`, `ICentroMaritimoReciboService`, `EstadoReciboToBrushConverter`,
  `ServiceFlowTests`) **volvieron a HEAD sin commit** (reversión externa a la sesión). El
  working tree auditado es el posterior a esa reversión.

## Resumen

| Prioridad | Cantidad | Descripción |
|---|---|---|
| **P0** | 0 | — |
| **P1** | 1 | Pérdida silenciosa de un cobro individual pendiente (ambas apps) |
| **P2** | 3 | NC sin registro ante fallo post-CAE · mail de consolidado incompleto en reintento · logs ciegos a rechazos AFIP |
| **P3** | 8 | PDF/template, docs desactualizados, red de tests, decisión sin registrar |

**Regresión de los fixes del 06-10: los 22 están VIGENTES** (verificación ítem por ítem
contra `fixing-log.md`, incluidos los archivos que D-21 volvió a tocar). La migración CM
regenerada **conserva** el índice de consolidados con filtro `AND "Estado" <> 'Anulado'`
(P1-3) y el índice único `(PuntoDeVenta, NumeroComprobante, CodigoAfip)` con
`NumeroComprobante > 0` (P2-5), y no tiene columnas de apoderado.

**D-21 (baja del apoderado + reorganización de Configuración): limpio.** Cero referencias a
apoderado en código, XAML, tests y migraciones (solo quedan menciones en docs históricos, ver
N-8). Bindings de las páginas re-armadas verificados uno por uno; los `RadioButton` sin
`GroupName` agrupan por contenedor (sin regresión); P1-8 y P3-12 sobreviven al re-armado.

---

## P1

### N-1 ✅ · Emitir un individual distinto con otro Pendiente del mismo período lo PISA: pérdida silenciosa de un cobro (ambas apps)

**Archivos:** `CamaraPortuariaReciboService.cs:198-218` y `CentroMaritimoReciboService.cs:328-348`
(`EmitirOResumirAsync`, rama resume) + `FiltrarPorClave` (CP `ReciboRepository.cs:43`,
CM `ReciboRepository.cs:44`).

**Problema:** para individuales (grupoId null), `GetPorClaveAsync` retorna el recibo
**Pendiente** de esa entidad+período (D-20) y el resume **re-sincroniza el snapshot con las
líneas del request actual**. Eso es correcto para reintentar el mismo cobro, pero si Laura
emite un individual **con otro contenido** (p.ej. quedó Pendiente "Papelería $5.000" porque
AFIP estaba caído, y después emite "Cobro extraordinario $20.000" a la misma entidad en el
mismo período), el segundo pedido **reemplaza las líneas y el importe del primero** y emite
un solo comprobante. El cobro original desaparece sin aviso: nunca se factura y nada lo
señala (el resultado es `Ok`).

**Evidencia de la semántica:** los tests `EmitirIndividual_DosVecesMismoPeriodo_CreaDosRecibos`
y `EmitirIndividual_ConPendientePrevio_RetomaSinDuplicar` fijan el comportamiento actual
(retomar al Pendiente), pero ningún test cubre el caso "Pendiente previo con contenido
DISTINTO" — que es donde el resume deja de ser un reintento y pasa a ser una sobreescritura.

**Fix propuesto:** en la rama resume de individuales, comparar el contenido (líneas e importe)
del Pendiente contra el request: si difieren, **crear un recibo nuevo** en vez de pisar el
snapshot (no hay índice único que lo impida; `ReintentarAsync` ya funciona por Id para el
reintento explícito). Mantener la re-sincronización solo para recibos de grupo (ahí el
contenido canónico es el del grupo). Agregar el test del caso divergente.

---

## P2

### N-2 ✅ (mitigado) · Nota de crédito: el CAE se pide ANTES de persistir — fallo post-CAE deja una NC autorizada sin registro local (ambas apps)

**Archivos:** `CamaraPortuariaReciboService.cs:341-359`, `CentroMaritimoReciboService.cs:552-570`.

El fix P1-2 hizo atómica la persistencia (recibo→Anulado + NC + desvinculación de vouchers en
un solo `SaveChanges` vía `AnularConNotaAsync`), pero el orden sigue siendo: **AFIP autoriza
la NC → recién después se persiste**. Si `AnularConNotaAsync` falla (lock de SQLite, disco,
crash), la NC existe fiscalmente con número y CAE consumidos pero la app no la conoce; un
reintento emitiría una **segunda NC** por el mismo recibo. Es la misma familia de riesgo que
P2-2 (timeout post-aprobación). **Fix:** patrón Pendiente-first también para NC (persistir la
NC sin CAE + recibo en estado transitorio, pedir CAE, completar), o como mínimo cubrirlo con
la reconciliación `FECompConsultar` (diseño abajo). Mientras tanto el riesgo es de muy baja
frecuencia (un solo SaveChanges local).

### N-3 ✅ · Reintento de consolidado CM con vouchers nuevos: el mail sale con el PDF único INCOMPLETO

**Archivo:** `CentroMaritimoReciboService.cs:137-160` (rama Pendiente existente) +
`:396-397` (mail del consolidado).

En el reintento de un consolidado Pendiente, los vouchers nuevos se vinculan vía
`_vouchers.MarcarConsolidadosAsync` (**otro DbContext**, transient). Las líneas y el importe
se recalculan bien (`todosVouchers = existente.Vouchers.Concat(vouchersAgencia)`), el CAE se
pide por el total correcto, pero el envío usa `recibo.Vouchers` — la colección del contexto
de `_recibos`, que **no ve los vouchers recién vinculados**. Resultado: la agencia recibe un
PDF único cuyo recibo totaliza N vouchers pero solo trae las páginas de los viejos (y el
`ResultadoCierrePorAgencia` reporta el count viejo). Un reenvío posterior desde Recibos sí
sale completo (recarga con `GetConDetalleAsync`).

Es la variante "colección stale entre contextos transient" de la familia del P0-1 — y los
tests no la ven porque el reintento CM solo está probado con contexto compartido (H-01,
fase 9), donde el fix-up de EF puebla la colección.

**Fix:** pasar `todosVouchers` al camino de envío (parámetro opcional de
`ProcesarReciboAsync` o recargar `existente` tras `MarcarConsolidadosAsync`), y agregar el
test de reintento CM **con DbContexts separados** (cierra también la brecha H-01).

### N-4 ✅ · El logger de archivo descarta todo lo que no traiga Exception: los rechazos de AFIP no dejan rastro en producción (ambas apps)

**Archivos:** `CamaraPortuaria.UI\Logging\FileLoggerProvider.cs:97` y gemelo CM
(`FileLogger.Log`: `if (exception is null) return;`).

Solo se escriben entradas con excepción. Pero los eventos más importantes para diagnosticar
producción se loguean **sin** excepción: `AfipService.cs:45` (`LogError` "AFIP rechazó el
comprobante … {Observaciones}"), `AfipService.cs:51` (`LogWarning` vencimiento de CAE
faltante), y cualquier `LogInformation`/`LogWarning` informativo. Un rechazo de AFIP en
producción no deja **ninguna** línea de log. Además `SetMinimumLevel(Debug)` en
`App.xaml.cs` es inocuo con este filtro.

**Fix:** en `FileLogger.Log`, escribir siempre que `logLevel >= Warning` (o `>= Information`)
aunque `exception` sea null; conservar el filtro solo para `Debug/Trace`. Nota doc: las
convenciones (`doc/arquitectura/convenciones.md:83-86`) y `estado-implementacion.md` dicen
"Serilog", pero la implementación real es este provider casero — actualizar (ver N-8).

---

## P3

| ID | Archivo(s) | Hallazgo y fix sugerido |
|---|---|---|
| N-5 | `Afip.Documentos/Pdf/ComprobanteTemplate.cs:447` | Si AFIP no devuelve vencimiento de CAE (`?? default`, fix P2-3), el PDF imprime **"01/01/0001"**. El QR no lo incluye (no afecta). Fix: guard `if (_doc.FechaVencimientoCae > DateTime.MinValue)` y omitir la línea. (La parte de PDF del P2-3 se saltó adrede en el fixing; cerrarla ahora.) |
| N-6 | `ComprobanteTemplate.cs:375-379` | El total del pie usa `_doc.ImporteTotal` sin validar contra `Sum(Items.Subtotal)` (= P3-19 diferido). Los servicios hoy los mantienen sincronizados; agregar verificación defensiva o test que ancle la igualdad. |
| N-7 | `CamaraPortuariaPdfService` / `CentroMaritimoPdfService` (NC) | Si `nc.ReciboOriginal` no vino cargado, la banda "Comprobante asociado" se omite y el receptor queda vacío, sin error. Hoy todos los callers lo cargan; agregar guard explícito (throw o log) para futuros consumidores. |
| N-8 | `doc/negocio/cierre-periodo.md` · `doc/arquitectura/convenciones.md:83` · `doc/negocio/funcionalidad-compartida.md:56` · `prompt_inicializacion.md` · `doc/mejoras/informe-auditoria.md` | Docs desactualizados: cierre-periodo.md aún dice que "Generar recibo"/"Cerrar período" son de una "próxima iteración" y que Anulado-como-Pendiente es un "caso a refinar" (ya implementado/corregido); convenciones/estado dicen "Serilog" (es `FileLoggerProvider` casero); funcionalidad-compartida define Vencido como "no Pagado/Anulado" pero el código exige Emitido/Enviado (`EstadoReciboHelper.cs:12-14` — excluye Pendiente, igual que el filtro del repo); los dos últimos aún describen el apoderado (históricos: marcar como tales o actualizar). |
| N-9 | `CentroMaritimo.UI/Views/VouchersPage.xaml` + `VoucherItem.cs` | La eliminación de la columna "Estado" de vouchers (y `EstadoTexto`) **no está registrada en ninguna decisión** (D-21 no la menciona) y es parte del episodio de archivos revertidos sin commit. La implementación quedó limpia (cero referencias colgantes), pero falta el registro: ampliar D-21 o crear D-22. |
| N-10 | `PuertoBB.Tests` | Red de tests (fase 9): sin tests `ReenviarMailAsync` (CP/CM), `MarcarPagadoAsync` happy path, `EmitirDeGrupoAsync` CM, `GetDuplicados/GetPendientes`; sin test de colisión del índice `(PV, Nro, CodigoAfip)`; sin test de fallo AFIP al pedir CAE de la NC (CP/CM); el patrón "contextos separados" solo cubre 2 tests (CP masivo + CM cierre simple) — extenderlo al reintento de consolidado (ver N-3), anulación y emisión individual CM. |
| N-11 | `AfipQrPayloadTests.cs` / `AfipDocumentosServiceTests.cs` | (= P3-20 diferido, confirmado) Falta test de QR con importe extremo (anclar que `decimal` no emite notación científica) y test de PDF multipágina (≥15 ítems) que ancle el footer CAE/QR en el salto de página. |
| N-12 | `CentroMaritimo.UI/Views/VouchersPage.xaml` | Cosmético: la barra de búsqueda quedó en `Grid.Row="2"` dejando el Row 1 vacío tras quitar la columna Estado. Sin efecto funcional. |

---

## Diseño propuesto — P2-2: reconciliación `FECompConsultar` tras timeout (para `Afip.Net`)

**Problema:** `WsfeService.SolicitarCaeAsync` (`Afip.Net/Wsfe/WsfeService.cs:32-67`) resuelve
`numero = FECompUltimoAutorizado + 1` y llama `FECAESolicitar`. Si la respuesta se pierde
(timeout post-aprobación), AFIP queda con el comprobante `numero` autorizado y la app lo
ignora; el reintento pide `UltimoComprobante` de nuevo (que ahora YA incluye el huérfano) y
emite `numero+1` → comprobante duplicado en los hechos y hueco lógico.

**Diseño (sin implementar):**

1. **Nuevo método en `IWsfeClient`/`IWsfeService`:** `ConsultarComprobanteAsync(options, pv,
   tipoCmp, numero)` → wrapper de `FECompConsultar` que devuelve los campos clave del
   comprobante autorizado (importe total, doc receptor, fecha, CAE, vencimiento).
2. **Reconciliación en `SolicitarCaeAsync`:** envolver la llamada `FECAESolicitar` en un
   catch de errores de transporte/timeout (`TimeoutException`, `CommunicationException`,
   `TaskCanceledException`). En el catch: consultar `FECompConsultar(pv, tipo, numero)` —
   el número que este mismo método acababa de calcular. Si existe y **coincide** en
   (ImporteTotal, DocNro, FechaComprobante) con el request → devolver ese CAE como
   `Aprobado` (reconciliado). Si no existe o no coincide → propagar el error original.
3. **Cobertura del reintento frío** (la app se cerró entre medio): en el camino de reintento
   de un recibo `Pendiente` cuyo `UltimoErrorCae` es de transporte, antes de pedir CAE nuevo,
   consultar el último autorizado y comparar contra el recibo; si coincide, adoptar
   CAE/número sin emitir. Esto puede vivir en `AfipService` (adaptador) para no acoplar
   `Afip.Net` al dominio.
4. **Tests:** mock de `IWsfeClient` que aprueba y luego tira timeout; verificar que el
   segundo intento devuelve el MISMO número/CAE y no emite dos veces. Cubre también el caso
   N-2 (NC) porque ambos pasan por `ObtenerCAEAsync`.

---

## Estado de diferidos (fase 10)

| Ítem | Clasificación | Nota |
|---|---|---|
| P2-2 (reconciliación timeout) | **Corregir para entrega** | Diseño listo (arriba). Es la única protección real contra duplicados fiscales por red. |
| P2-10 (datos reales en seed) | **CERRADO** | Decisión del usuario 2026-06-11: se dejan como están (repo privado; el seed solo corre en modo demo). Registrar en `registro-decisiones.md` durante el fixing. |
| P3-7 (`Math.Round` antes del cast a double en `WsfeMapper`) | **Corregir para entrega** | One-liner; elimina un riesgo teórico en importes. |
| P3-15 (visor PDF embebido CM→CP + limpiar temporales) | Opcional | Mejora UX; no bloquea entrega. Los PDFs temporales de CP sí conviene limpiarlos (one-liner al cerrar). |
| P3-19 / N-6 (total del template confía en el caller) | **Corregir para entrega** (parte barata) | Validación defensiva o test de igualdad; el resto (magic string "RECIBO") aceptado. |
| P3-20 / N-11 (tests QR extremo + multipágina) | **Corregir para entrega** | Tests baratos que anclan contratos fiscales. |
| P3-4 (tracking en `GetByIdAsync`) · P3-5 (queries sin `Lineas`) · P3-6 (decimal TEXT) · P3-13 (notificación manual) · P3-14 (mails sin progreso) · P3-17 (carrera contador voucher) · P3-21 (`ImporteNuevo` sin reset) · P3-22 (archivos >600 líneas) | **Aceptados/documentar** | Sin impacto funcional real para v1; P3-6 y P3-17 ya tienen mitigación (índices únicos / convención). P3-21: documentar que es intencional por `ImporteVoucherPredeterminado`. |

---

## Checklist de entrega (fase 11)

| # | Ítem | Estado | Acción concreta |
|---|---|---|---|
| 1 | **Distribución / instalador** | ⏭️ Fuera de alcance | Decisión del usuario (2026-06-11): se resuelve en una pasada futura. Mecanismo sugerido cuando se retome: `dotnet publish -c Release -r win-x64 --self-contained true` + ZIP. |
| 2 | **Procedimiento de paso a producción** | ✅ | `doc/usuario/paso-a-produccion.md` (fixing 06-11): conmutación, checklist de primera corrida, datos/logs/backup y limitación de reconciliación en frío. |
| 3 | **Primera corrida sin seed** | ⚠️ Aceptable con doc | Con `ModoDemo=false` la base nace vacía (solo `Configuracion` Id=1 y `ContadorVoucher` Id=1 sembrados). Los guards existen y son claros ("Configure un punto de venta activo…", "No hay certificado AFIP configurado…"). No hay wizard de onboarding: cubrirlo con el manual (orden: Emisor → Punto de venta + certificado → Probar conexión → Correo → entidades). |
| 4 | **Prueba AFIP real (homologación)** | ⚠️ Pendiente externa | Mapeo/firma testeados; falta la corrida con `.p12` real. Guía ya existe: `doc/usuario/afip-configuracion.md`. Es el único bloqueante técnico-externo para facturar. |
| 5 | **SMTP real** | ⚠️ Pendiente externa | Cargar host/credenciales en Configuración y probar un envío. Password ya cifrada en reposo (P2-11). |
| 6 | **Licencia QuestPDF** | ✅ | `Settings.License = Community` en `Afip.Documentos/DependencyInjection.cs:11`, invocado por `AddPuertoBBPdf()` en ambas apps. |
| 7 | **Logs** | ✅ | Retención OK (rolling diario, 30 archivos). N-4 corregido: se escribe Warning+ siempre (rechazos AFIP incluidos). |
| 8 | **Backup** | ✅ / ⚠️ | Backup + restore implementados y arreglados (P1-5/P2-9); `doc/usuario/base-de-datos.md` existe. Agregar al manual la recomendación de frecuencia y dónde guardar el archivo. |
| 9 | **Manual operativo de usuario** | ✅ | `doc/usuario/manual-camara-portuaria.md` y `manual-centro-maritimo.md` (fixing 06-11). |
| 10 | **Versión visible / About** | ✅ | `<Version>1.0.0</Version>` en ambos csproj + versión en el título de la ventana (` · v1.0.0`). |
| 11 | **Datos demo con CUITs reales** | ✅ Cerrado | Decisión P2-10: quedan (repo privado, seed solo en demo). |
| 12 | **Bases `.db` de dev tras regenerar la migración CM** | ⚠️ Verificar a mano | D-21 dice que se borraron; con la app abierta se recrean. Antes de entregar, borrar `%LocalAppData%\PuertoBB\*` en la máquina de dev y verificar arranque limpio. |

**Camino crítico sugerido para "entregable":** corregir N-1, N-3, N-4 (+ N-2 si entra
P2-2) → cerrar ítems 1, 2, 9 y 10 del checklist (distribución, doc de paso a producción,
manual, versión) → prueba AFIP homologación + SMTP reales (ítems 4 y 5, requieren tus
credenciales) → smoke test manual final con `ModoDemo=false` contra homologación.

---

## Revisado y OK (no re-mirar)

Hereda la lista de `AUDITORIA-2026-06-10.md` (AFIP/WSAA, PDF desde `Lineas`, EF, async/UI,
seed, convenciones, paridad CP↔CM) y suma lo verificado en esta pasada:

- **Los 22 fixes del 06-10 vigentes** (tabla completa en fase 3; sin regresiones). Único
  residuo cosmético: el docstring de `TraBuilder` aún dice "SHA1+RSA" (es SHA256).
- **Migración CM regenerada**: índices P1-3/P2-5 preservados, sin columnas de apoderado,
  snapshot == modelo (has-pending-model-changes limpio en ambos contextos).
- **Baja del apoderado**: cero referencias en código/XAML/tests/migraciones; docs de
  negocio/arquitectura/usuario actualizados en el mismo diff (salvo N-8).
- **Reorganización de Configuración (ambas apps)**: todos los bindings/commands resueltos;
  RowDefinitions consistentes; radios agrupan por contenedor; P3-12 (desuscripción en
  Unloaded) vigente.
- **Vouchers sin columna Estado**: implementación limpia (sin referencias colgantes);
  `Consolidado` sigue gobernando editar/eliminar; converters CP↔CM byte-idénticos y cubren
  los 8 estados visibles.
- **PDF**: ítems desde `Lineas` y receptor desde snapshot en TODOS los caminos (individual,
  consolidado, NC, preview, descarga, mail); emisor con IngresosBrutos/InicioActividades;
  banda de comprobante asociado en NC; QR completo; consolidado siempre vía
  `GenerarPdfDescargaAsync`; moneda/fecha con cultura explícita.
- **Comportamiento D-20 verificado por tests**: dos individuales completos conviven en el
  mismo período; individual no choca con consolidado CM (la excepción es N-1).
- **QuestPDF Community**, **retención de logs**, **guards de PV activo y certificado**,
  **banner MODO DEMO** (título) y **appsettings.json** conmutables — todos verificados.

## Orden de corrección sugerido

1. **N-1** (pisado de individuales) + test del caso divergente — ambas apps.
2. **N-3** (mail de consolidado incompleto) + test de reintento CM con contextos separados
   (cierra la brecha H-01 de una vez).
3. **N-4** (logger) — one-liner con efecto grande en producción.
4. **P2-2** según el diseño de este informe (cubre también el residuo N-2).
5. P3 baratos en bloque: N-5 (guard vencimiento CAE), P3-7 (`Math.Round`), N-6/P3-19
   (validación de total), N-11/P3-20 (tests QR/multipágina), N-9 (registrar decisión),
   N-8 (docs).
6. Checklist de entrega: ítems 1, 2, 9, 10 (distribución, procedimiento, manual, versión);
   después las pruebas reales AFIP/SMTP (4, 5) y el smoke final.

> Verificación post-fixing recomendada: `/validar-plataforma` + smoke manual con
> `ModoDemo=false` contra homologación: (a) emisión masiva CP, (b) cierre CM → anular →
> reemitir → reintento con voucher nuevo agregado, (c) backup/restore, (d) revisar que el
> log registre un rechazo AFIP simulado.
