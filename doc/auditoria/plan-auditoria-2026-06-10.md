# Plan de Auditoría Extensiva — PuertoBB (2026-06-10)

> **Cómo ejecutarlo:** en una sesión nueva de Claude Code, decir:
> *"Ejecutá el plan de auditoría de `doc/auditoria/plan-auditoria-2026-06-10.md` de punta a punta y generá el informe."*

**Objetivo:** revisar todo el working tree (incluye cambios sin commitear) y producir
`doc/auditoria/AUDITORIA-2026-06-10.md` con hallazgos priorizados (bugs + mejoras),
cada uno con archivo:línea, descripción, impacto y fix propuesto. **No se modifica código** —
el informe es el insumo para correr las correcciones después.

**Línea base ya verificada (2026-06-10):**
- `dotnet build PuertoBB.slnx` → 0 errores, 0 warnings.
- `dotnet test PuertoBB.slnx` → 74/74 verdes (16 Afip.Documentos.Tests + 58 PuertoBB.Tests).
- Tamaño: 291 archivos `.cs` (~25k líneas, parte es `Afip.Net/Soap/Generated` — excluir de greps de calidad), 36 `.xaml`.

## Fases

1. **Línea base** — re-verificar build (capturar warnings) + corrida completa de tests.
2. **Concurrencia y async** — `async void` (ya detectados: `App.xaml.cs OnStartup/OnExit` en ambas apps,
   `AsyncRelayCommand.Execute`, `PdfPreviewDialog.OnLoaded` — verificar que tengan try/catch),
   `.Result`/`.Wait()`, fire-and-forget, `Task.Run` mal usado, acceso a UI desde threads,
   DbContext compartido entre tareas concurrentes.
3. **Capa de datos (EF Core)** — lifetime de DbContext, tracking innecesario, N+1,
   transacciones faltantes en operaciones multi-entidad (emisión masiva, cierre de período),
   migraciones vs modelo actual (¿pending model changes?), índices/constraints únicos
   (numeración de recibos), borrado en cascada, concurrencia optimista.
4. **Lógica de negocio (Core/Services)** — redondeo decimal (2 decimales, MidpointRounding),
   numeración de comprobantes (huecos/duplicados), estados de recibo y transiciones válidas,
   períodos/fechas, validación CUIT, consistencia entre las dos verticales
   (CamaraPortuaria vs CentroMaritimo — lógica que divergió sin razón), catálogo AFIP
   (`CatalogoComprobantesAfip` como única fuente; Recibo C = 15).
5. **AFIP (Afip.Net + adaptador)** — manejo de errores SOAP (observaciones vs errores),
   cache de ticket por servicio (expiración, relojes), seguridad del certificado
   (DPAPI, password cifrada en reposo, no loguear secretos), reintentos/timeout,
   ambiente homologación vs producción, CAE y fecha de vencimiento bien persistidos.
6. **PDF (Afip.Documentos)** — generación SIEMPRE desde `Lineas` (modelo unificado de recibos),
   QR AFIP (formato URL/base64 del JSON), datos obligatorios del comprobante, totales = suma de líneas.
7. **UI / MVVM (ambas apps)** — bindings rotos o sin `Mode` correcto, converters,
   comandos sin `CanExecute`/doble click, manejo de excepciones en handlers de eventos,
   duplicación entre las dos apps (candidatos a extraer a común), `SeedData`/`App.ModoDemo`
   filtrándose a producción, preview de consolidado usando `GenerarPdfDescargaAsync`.
8. **Seguridad y robustez** — secretos hardcodeados en código/config, logs con datos sensibles
   (password SMTP, password del certificado), manejo de archivos (rutas, colisiones, permisos),
   backup de la base (VACUUM INTO — ¿se valida el destino?), validación de inputs de usuario.
9. **Calidad general** — código muerto (p.ej. restos de `PreviaEmisionItem` borrado),
   TODO/FIXME reales, duplicación, disposal de recursos (streams, `HttpClient`, `SmtpClient`),
   excepciones tragadas (`catch` vacío o solo log donde debería propagar).
10. **Coherencia entre las dos verticales** — comparación sistemática lado a lado de
    CamaraPortuaria vs CentroMaritimo en cada capa: entidades (`PuertoBB.Core/Entities/*`),
    configuraciones EF, repositorios, servicios de recibo, ViewModels y Views homólogos.
    Para cada divergencia, clasificar: (a) justificada por el negocio (Empresa vs Agencia/Barco),
    (b) bug — una app tiene un fix o mejora que la otra no recibió, o (c) duplicación que
    debería extraerse a código compartido. Las divergencias tipo (b) son hallazgos P1.
11. **Modelo de datos** — revisión dedicada del modelo completo contra `doc/negocio/` y
    `doc/arquitectura/datos.md`: tipos y precisión de decimales (importes), nullability
    coherente entre entidad y configuración EF, relaciones y navegaciones (multiplicidad,
    cascadas), enums vs strings para estados, campos obligatorios del comprobante AFIP
    persistidos (CAE, vencimiento, tipo, punto de venta, número), claves únicas que
    garanticen la numeración, y datos que el negocio exige y el modelo no captura.
12. **Flujos de interacción completos** — trazar de punta a punta cada flujo de negocio y
    verificar en el código cada paso, sus transiciones de estado y qué pasa si falla a mitad
    de camino (¿queda estado inconsistente?, ¿se puede reintentar?):
    - Alta/edición/baja de Empresa (CP) y Agencia+Barco (CM), con validaciones.
    - Generación de recibo individual, por grupo multi-ítem y emisión masiva.
    - Emisión AFIP: pedido de CAE, manejo de rechazo/observaciones, reintento, NC derivada.
    - Generación de PDF (descarga y preview de consolidado) y envío por mail.
    - Control de pagos: registro, estados, anulación.
    - Cierre de período (CM): qué bloquea, qué recalcula, reversibilidad.
    - Configuración: cambio de certificado AFIP, probar conexión, cambio de comprobante a emitir.
13. **Buenas prácticas y convenciones del proyecto** — verificar el código contra
    `doc/arquitectura/convenciones.md` (naming, estructura MVVM, dónde vive cada cosa)
    y las recetas de `doc/arquitectura/receta-entidad-end-to-end.md`; marcar las secciones
    que se desviaron del patrón establecido.
14. **Informe final** — consolidar en `doc/auditoria/AUDITORIA-2026-06-10.md` con formato:
    - Tabla resumen por prioridad y luego una sección por hallazgo:
      `ID | Prioridad | Archivo:línea | Título`, descripción, impacto, fix propuesto.
    - Prioridades:
      - **P0** — bug que corrompe datos / rompe emisión AFIP / crash.
      - **P1** — bug funcional o riesgo real en producción.
      - **P2** — mejora de robustez / deuda técnica importante.
      - **P3** — mejora menor / cosmética.

## Estrategia de modelos (abaratar la corrida)

Ejecutar con subagentes (tool Agent) usando el modelo más barato que cada fase tolera.
Los subagentes baratos **recolectan** hallazgos crudos; el modelo principal **verifica**
todo P0/P1 leyendo el código él mismo antes de que entre al informe (los modelos chicos
tienen más falsos positivos — ningún hallazgo de un subagente entra sin verificar).

| Fase | Modelo | Por qué |
|---|---|---|
| 1. Línea base (build+tests) | haiku | Correr comandos y copiar resultados. |
| 2. Async/concurrencia | sonnet | Barrido de patrones + lectura puntual. |
| 3. EF Core | sonnet | Detección de patrones; los casos dudosos los re-verifica el principal. |
| 4. Lógica de negocio | **principal** | Requiere criterio de negocio (plata, numeración, estados). |
| 5. AFIP | **principal** | Crítico: errores acá rompen la facturación real. |
| 6. PDF | sonnet | Verificación mecánica contra el modelo unificado. |
| 7. UI/MVVM | sonnet | Bindings y converters son chequeo de patrones; volumen alto. |
| 8. Seguridad | **principal** | Falsos negativos caros; mejor el modelo fuerte. |
| 9. Calidad general | haiku | TODO/FIXME, código muerto, disposal: mecánico. |
| 10. Coherencia entre verticales | sonnet (recolección) + principal (clasificación) | El diff lado a lado es mecánico; decidir si la divergencia es bug requiere criterio. |
| 11. Modelo de datos | **principal** | Contrasta contra negocio documentado. |
| 12. Flujos completos | **principal** | Razonamiento sobre estados y fallas a mitad de camino. |
| 13. Convenciones | haiku | Comparar contra `convenciones.md` es mecánico. |
| 14. Informe final | **principal** | Consolidación, deduplicación y priorización. |

Orquestación:
- Lanzar las fases de subagente en paralelo (independientes entre sí) con
  `model: haiku` o `model: sonnet` según la tabla; cada una escribe su salida cruda en
  `doc/auditoria/raw/fase-NN.md` con formato `archivo:línea — hallazgo — evidencia`.
- Cada prompt de subagente debe ser autocontenido (qué buscar, qué excluir, dónde escribir)
  porque el subagente arranca sin el contexto de esta sesión.
- El modelo principal corre sus fases propias mientras esperan los subagentes, después
  verifica los crudos y arma el informe final. Borrar `doc/auditoria/raw/` al terminar.

## Reglas de ejecución

- Antes de las fases de negocio (4, 10, 11 y 12), leer `doc/negocio/` completo y
  `doc/arquitectura/datos.md` + `flujos.md` para auditar contra lo documentado, no contra supuestos.
- Solo lectura: no editar código, no tocar git. El único output es el informe (y este plan).
- Excluir `Afip.Net/Soap/Generated/**` de los análisis de estilo/calidad (es código generado).
- Cada hallazgo debe ser accionable en frío: con archivo:línea y fix concreto, sin depender
  del contexto de la sesión que lo encontró.
- Al final, listar también lo que se revisó y quedó OK (para saber qué no hace falta re-mirar).
