# Informe de auditoría — PuertoBB (en construcción)

> **Documento histórico** (2026-06-08, anterior a D-21): superado por las auditorías
> formales de `doc/auditoria/` (informes 2026-06-10 y 2026-06-11). Las menciones al
> "apoderado fiscal" ya no aplican: el feature fue eliminado (D-21).

> Generado por el trabajo nocturno autónomo. Se completa de forma incremental por el loop.
> Estado: **FASE 1 en curso.** Última actualización: 2026-06-08 (iteración 1).

## Resumen ejecutivo (preliminar)

- **Baseline:** Build ✅ 0 warnings / 0 errores. Tests ✅ 63 (47 PuertoBB.Tests + 16 Afip.Documentos.Tests). Migraciones EF en sync en ambos contextos (CP/CM).
- **Riesgos principales confirmados:** HK-1 (detalle del recibo inconsistente según pantalla) y HK-2 (recibo de un solo ítem) tienen evidencia dura (ver abajo). HK-3/HK-4 son el rediseño que los resuelve.
- **Homogeneización CP↔CM:** ya aparecen divergencias concretas (largo de `Detalle`, PDF que deriva el detalle solo en CM). A relevar en profundidad en próximas iteraciones.

## Hallazgos confirmados con evidencia

| # | Sev | Categoría | Ubicación | Problema | Evidencia | Corrección |
|---|-----|-----------|-----------|----------|-----------|------------|
| F-01 | 🔴 | AFIP/Negocio (HK-1) | `PuertoBB.Services/Pdf/CentroMaritimoPdfService.cs:159` | El PDF de CM **no usa `recibo.Detalle`** cuando el recibo tiene vouchers: `ConceptoGeneral = vouchers.Count == 0 ? recibo.Detalle : null`. El detalle se **deriva** de los vouchers en runtime, distinto del `Detalle` persistido que se ve en otras pantallas. | línea 159 | Snapshot de ítems persistido (HK-3); el PDF y toda la UI leen del snapshot, no recalculan. |
| F-02 | 🟠 | Homogeneización (Datos) | `Infrastructure/Data/Configurations/CamaraPortuaria/ReciboConfiguration.cs:13` vs `.../CentroMaritimo/ReciboConfiguration.cs:13` | `Recibo.Detalle` tiene `HasMaxLength(1000)` en CP y `HasMaxLength(2000)` en CM. Divergencia sin justificación. | CP=1000, CM=2000 | Unificar criterio (o documentar la diferencia). Con HK-3 el detalle pasa a ítems; revisar largo por ítem. |
| F-03 | 🔴 | Negocio (HK-2) | `Core/Entities/*/Recibo.cs` (`decimal Importe` + `string Detalle`) | El recibo soporta **un solo detalle + un total**; no existe entidad de ítems/líneas. | entidades CP/CM | Entidad de ítems del recibo + total derivado (HK-3). |
| F-04 | 🔴 | Negocio (HK-4) | `Core/Entities/*/NotaDeCredito.cs` | La NC **no persiste detalle/importe/ítems**: solo `ReciboOriginalId` + datos AFIP. Su detalle se deriva del recibo original → arrastra HK-1. | entidades CP/CM | NC con snapshot propio copiado del recibo; **solo anulación total** (decisión fijada). |

## Paso 1 — Reglas de dependencias / arquitectura (chequeo estático)

- ✅ `MessageBox` solo en `App.xaml.cs` de cada app (handler de último recurso, permitido por convención). Resto vía `IDialogService`.
- ✅ Cero `Console.WriteLine` / `Debug.WriteLine` en toda la solución.
- ✅ ViewModels sin `DbContext` (no hay acceso directo a datos desde la UI).
- ✅ **F-05 CERRADO** (iter 30, sin cambio de código): auditados todos los `try/catch` en VMs. **No hay violaciones reales**: capturan errores de **repositorio CRUD simple** (Agencias/Barcos/Conceptos, sin Service dedicado) y de **PDF/archivo** (previsualizar/descargar/guardar). **Ninguno** envuelve una llamada de negocio del `ReciboService` (esas usan `ServiceResult` + `res.Success`). Se **refinó la convención** (`convenciones.md`) para documentar esta excepción permitida (infra/IO sin Service), en vez de hacer un refactor riesgoso. Paridad CP↔CM OK.

## Paso 2 — Core (entidades, enums, DTOs)

Comparación CP↔CM (alto nivel de homogeneidad, divergencias justificadas por dominio salvo lo indicado):
- ✅ `ConceptoRecibo`: idéntico.
- ✅ `GrupoFacturacion`: idéntico salvo navegación `Empresas` (CP) vs `Agencias` (CM) — justificado.
- ✅ `PuntoDeVenta`: byte-idéntico.
- ✅ `Configuracion`: CM agrega `UsarApoderado/NombreApoderado/CuitApoderado` (apoderado fiscal) e `ImporteVoucherPredeterminado` — propios del dominio CM, justificado.
- ✅ **F-06 CERRADO (decisión del usuario, 2026-06-08):** no se cifra nada por ahora — ni SMTP ni AFIP. SMTP queda en texto plano (aceptable para app unipersonal). El cifrado DPAPI existente del certificado AFIP **se deja como está** (no se remueve, para no romper contraseñas ya guardadas). Sin acción de código.

- ✅ **F-19 RESUELTO** (iter 30): cultura es-AR fijada al arranque en CP+CM (`ConfigurarCultura`: `DefaultThreadCurrentCulture` + `FrameworkElement.Language`), así las grillas XAML (`StringFormat=C2/d`) y el helper `Formato` formatean igual sin depender del SO.
- ✅ **F-02 RESUELTO** (iter 30): `Recibo.Detalle` unificado a `HasMaxLength(2000)` en CP (= CM) + migración `UnificarLargoDetalleRecibo`. En sync.
- Pendiente fino: comparar `Empresa`/`EmpresaGrupo` (CP) vs `Agencia`/`AgenciaGrupo`/`Barco` (CM); revisar enums (`ReciboEstado`, `TipoComprobante`, `TipoEntidad`) y DTOs en `Models/` por duplicación.

## Paso 3 — Infrastructure (EF / repos)

- ✅ `RepositoryBase`: `AsNoTracking()` en `GetAllAsync`; escritura envuelve `DbUpdateException` en `ReciboException` con log (cumple `convenciones.md`). `GetByIdAsync` usa `FindAsync` (tracking, correcto para editar).
- ✅ Repos concretos CP↔CM consistentes: lecturas con `AsNoTracking` + `Include` explícito (Recibo→Empresa/Agencia/Grupo, Voucher→Barco). No se observan N+1 evidentes a nivel repo (a confirmar en services al iterar colecciones).
- 🟢 **F-07** (Datos, no-bug en SQLite): no hay `HasPrecision`/`HasColumnType` en columnas `decimal` (`Importe`). En SQLite EF Core mapea `decimal`→`TEXT` y preserva el valor, así que **no hay pérdida de precisión**. Documentar la decisión; si alguna vez se migra a otro proveedor, agregar precisión explícita.
- 🟢 **F-08** (Consistencia): `RepositoryBase` setea `CreatedAt/UpdatedAt = DateTime.Now` (hora local). Aceptable en app unipersonal; considerar `DateTime.UtcNow` para consistencia. Mantener criterio único en toda la solución.
- Pendiente fino: revisar índices únicos y parciales (consolidados CM) en las `*Configuration`, y `DeleteBehavior` de FKs.

## Paso 4 — Services (en curso)

### CamaraPortuariaReciboService
- ✅ Manejo de errores correcto: `EmitirOResumirAsync` envuelve en try/catch → `ResultadoEmisionPorEntidad.Fallo` (no propaga). Fallo de mail = `LogWarning`, recibo queda `Emitido` (cumple convención). CAE idempotente (no se re-pide si ya existe).
- ✅ `AnularReciboAsync` = **anulación total** (NC por `recibo.Importe`, recibo→`Anulado`, `ComprobanteAsociado` con tipo/PV/número/CUIT emisor). Coincide con la decisión de negocio fijada.
- ✅ **F-09 RESUELTO** (iter 22): `MarcarPagadoAsync` (ambas apps) ahora rechaza `Anulado` y `Pendiente`. Falta test (L-G).
- ✅ **F-10 RESUELTO** (iter 22): `AnularReciboAsync` (ambas apps) ahora exige `CAE` presente antes de anular. Falta test (L-G).
- 🔗 Refuerza **F-04**: la `NotaDeCredito` creada acá no copia detalle/ítems; el PDF de NC depende de `nota.ReciboOriginal`. Con HK-3/HK-4 debe copiar el snapshot.
### CentroMaritimoReciboService (comparado con CP)
- ✅ Estructura bien alineada con CP (`EmitirOResumirAsync`, `ProcesarReciboAsync`, `EsCompleto` equivalentes; `AnularReciboAsync` casi idéntico). CM agrega cierre de período + consolidación de vouchers (propio del dominio).
- 🔴 **HK-1 causa raíz confirmada**: `CentroMaritimoReciboService.cs:129` arma `detalle = "Vouchers Nros: " + string.Join(...)` y lo persiste en `recibo.Detalle`, pero `CentroMaritimoPdfService.cs:159` **ignora** `recibo.Detalle` cuando hay vouchers y renderiza la lista de vouchers aparte → doble fuente del detalle. La solución es el snapshot de ítems (HK-3).
- ⚠️ **F-09 y F-10 también presentes en CM** (`MarcarPagadoAsync` sin guarda; `AnularReciboAsync` sin exigir CAE). Son bugs **compartidos** CP↔CM: corregir en ambos a la vez (buena paridad, mala lógica).
- ⚠️ **F-11** 🟡 (Homogeneización/DRY): `AnularReciboAsync`, el armado del `ComprobanteAfipRequest` y `EsCompleto` están casi duplicados entre los dos services. Evaluar extraer a helper/base compartida en `Services` (respetando que Empresa/Agencia son tipos distintos).
### AfipService (adaptador IAfipService → Afip.Net)
- ✅ Limpio: try/catch → `ServiceResult.Fail` con log; rechazo AFIP logueado con observaciones (`AfipErrores.Describir`); `VerificarServicioAsync`/`ProbarConexionAsync` con diagnóstico claro; valida certificado disponible antes de llamar.
- ✅ **F-12 RESUELTO** (iter 23): `ObtenerCAEAsync` valida CUIT receptor y CUIT del comprobante asociado con `TryParse` → error claro; `ToAfipRequest` defensivo con `TryParse`. Build + 47 tests verdes.

### PdfService (CP vs CM)
- 🔴 **HK-1 completo (capa PDF)**: CM `CentroMaritimoPdfService` arma `Items` desde la colección `vouchers` (`Voucher {Numero} — {Barco} — {Fecha}`) y pone `ConceptoGeneral = vouchers.Count == 0 ? recibo.Detalle : null`. El mismo recibo, si se genera por el PDF simple (`GenerarPdfReciboAsync`), muestra `recibo.Detalle` = "Vouchers Nros: 1,2,3". Dos representaciones del mismo comprobante → exactamente HK-1.
- 🟠 **F-13** (HK-2/Homogeneización): `CamaraPortuariaPdfService.GenerarPdfReciboAsync` **no soporta ítems** (solo `ConceptoGeneral = recibo.Detalle`, una línea); CM sí usa `Items`. Para HK-3 (recibo multi-ítem) hay que agregar render de ítems en el PDF de CP.
- 🟡 **F-14** (HK-4): el PDF de NC en ambas apps (`GenerarPdfNotaDeCreditoAsync`) solo pone `ImporteTotal` + leyenda "Anula el recibo original Nro. X", **sin ítems/detalle** de lo que se acredita. Con el snapshot de HK-4 debe replicar los ítems del recibo.
- 🟢 **F-15** (Pulido): `ColorAcentoHex` hardcodeado en los PdfService (`#1565C0` CP, `#00695C` CM) habiendo `PdfTheme.cs`. Centralizar en el theme.

### Estado Paso 4
Cerrado en lo esencial (ReciboService CP/CM, AfipService, PdfService). Pendiente menor: `VoucherService`, `Formato`/`PeriodoHelper` (revisión rápida de cultura/formato en Paso 8).

## Paso 6.bis — Iconos y tooltips de botones de operación (en curso)

- ✅ Buena base: los botones de operación usan estilo compartido `AccionIconButton` + `ui:SymbolIcon`, con iconos semánticos consistentes.
- ✅ Paridad fuerte: `ControlPagosPage` es **byte-idéntica** entre CP y CM.
- ⚠️ **F-16** 🟡 (Pulido — pedido explícito del usuario): los botones de operación tienen `Content` (texto) + icono pero **no tienen `ToolTip`**. Agregar tooltips homogéneos a todos los botones de operación (Vouchers, Cierre de período, Recibos, Emisión masiva, Control de pagos, y resto).
- Pendiente: revisar botones de acción **por fila** en `RecibosPage`/`VouchersPage`/`CierrePeriodoPage` (suelen ser icon-only, donde el tooltip es imprescindible) y completar el catálogo de abajo.

### Catálogo canónico operación → icono → tooltip (en construcción)

| Operación | Icono (WPF-UI Symbol) | ToolTip sugerido | Visto en |
|-----------|------------------------|------------------|----------|
| Actualizar/Reintentar | `ArrowSync24` | "Actualizar la lista" / "Reintentar…" | ControlPagos, Recibos |
| Buscar | `Search24` | "Buscar…" | Recibos |
| Marcar pagado | `CheckmarkCircle24` | "Marcar como pagado" | ControlPagos, Recibos |
| Reenviar mail | `Mail24` | "Reenviar por correo" | ControlPagos, Recibos |
| Anular | `Prohibited24` | "Anular emitiendo nota de crédito" | Recibos |
| Previsualizar/PDF | `DocumentPdf24` | "Abrir el PDF" | Recibos |
| Emitir | **`Receipt24`** (canónico) | "Emitir el recibo" | Recibos |
| Emitir y enviar | `Send24` | "Emitir y enviar por mail" | Recibos |
| _(completar: Editar, Eliminar, Agregar, Cerrar período, Backup, Probar conexión, Cargar voucher)_ | | | |

✅ **F-18 RESUELTO** (iter 21): "Previsualizar" unificado a `DocumentPdf24` en Vouchers y CierrePeriodo (CM) para igualar a Recibos. Criterio: previsualizar PDF = `DocumentPdf24`.

✅ **F-17 RESUELTO** (iter 21): EmisiónMasiva (ambas apps) renombrado de "Emitir" a **"Emitir y enviar"** (el comando emite + envía mail), manteniendo `Send24`. Criterio homogéneo final: `Receipt24` = Emitir (sin mail), `Send24` = Emitir y enviar.

## Paso 5 — Afip.Net / Mock / Documentos (auditado, OK)
- ✅ `TraBuilder`: TRA con `uniqueId` monótono (`Interlocked`), `generationTime -10min`/`expirationTime +10min`, firma CMS PKCS#7; soporta P12 (password) y CRT+KEY (PEM); flags `MachineKeySet|PersistKeySet|Exportable`. 🟢 nota: el comentario dice "SHA1+RSA" pero `CmsSigner` usa SHA256 por defecto (AFIP lo acepta) — corregir comentario algún día.
- ✅ `TicketCache`: thread-safe (semáforo por `cuit:servicio` + double-check), margen de 10 min. Correcto.
- ✅ `AfipQrBuilder`/`AfipQrPayload`: URL `https://www.afip.gob.ar/fe/qr/?p=<base64>` con JSON conforme a la spec (ver/fecha/cuit/ptoVta/tipoCmp/nroCmp/importe/moneda PES/ctz/tipoDocRec/nroDocRec/tipoCodAut "E"/codAut).
- ✅ `MockWsfeClient`: numeración secuencial por (PV, tipo), fiel a `IWsfeClient`.
- ✅ `WsfeMapper`: tipo C sin array IVA, `ImpNeto=0` (regla crítica, error 10071 evitado).

## Paso 7 — Tests (reforzado)
- ✅ Agregados tests de las guardas: `Anular_ReciboSinCae_Falla` (F-10) y `MarcarPagado_ReciboAnulado_Falla` (F-09), + `EmitirMasivo_PersisteLineasSnapshot` (HK-3). Total **50 tests verdes**.
- Pendiente (opcional): test de cierre CM = 1 línea por voucher; tests de paridad CM↔CP de anulación.

## Paso 8 — Transversal (revisado)
- ✅ Async: sin `.Result`/`.Wait()`; `async void` solo en `App.OnStartup/OnExit` (patrón framework, aceptable).
- ✅ Sin `Console.WriteLine`/`Debug.WriteLine`. ✅ Cultura es-AR fijada (F-19).
- ✅ `IDisposable`: `MailService` usa `using` para el `SmtpClient`; streams de logo/PDF con `using`.
- 🟢 Observación: tras HK-3, `CentroMaritimoPdfService.GenerarPdfConsolidadoAsync` ya no usa `vouchers` para el comprobante (el detalle sale de `Lineas`); el parámetro queda por compatibilidad del merge. Sin impacto.

## Cleanups aplicados
- ✅ **F-15**: acento del PDF centralizado en `PdfTheme` (CP+CM usan `_theme.AcentoHex`, sin hex hardcodeado).
- ✅ **F-11** (parcial seguro): `EsCompleto` extraído a `EstadoReciboHelper.EsCompleto` (compartido CP/CM). La dedup del armado AFIP de la NC se **deja** (camino crítico difícil de testear; mal trade-off de riesgo).
- ✅ **TODO `VoucherService:138`**: reemplazado por nota de decisión (anulado→pendiente para reemisión es intencional).

## Pendiente de auditar (próximas iteraciones del loop)

- Paso 2 — Core: comparación campo a campo entidades CP↔CM; enums; DTOs duplicados.
- Paso 3 — Infrastructure: índices, precisión decimal de importes, AsNoTracking, N+1, repos CP↔CM.
- Paso 4 — Services: ServiceResult/errores/logging; comparación de los dos ReciboService y PdfService; AFIP.
- Paso 5 — Afip.Net / Mock / Documentos.
- Paso 6 — UI: DI, MVVM, XAML, bindings, MessageBox; comparación página a página CP↔CM.
- Paso 6.bis — Catálogo iconos/tooltips de botones de operación.
- Paso 6.ter — Paridad funcional CM→CP.
- Paso 7 — Tests: huecos de cobertura.
- Paso 8 — Transversal: nullability, async, IDisposable, cultura/formato, seguridad, código muerto.
- Paso 9 — Plan de documentación + skills.

## Notas para la Fase 2

- F-01/F-03/F-04 se resuelven juntos con el agregado Recibo+ítems (L-D) y NC snapshot (L-E).
- F-02 es quick win de homogeneización (L-B), pero coordinar con L-D.
