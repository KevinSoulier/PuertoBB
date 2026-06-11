# Orquestación nocturna — Auditoría + Corrección autónoma (PuertoBB)

> Prompt maestro por pasos para trabajo desatendido (toda la noche). Primero **audita**, después **aplica todo**.
> Estado/decisiones del usuario (FIJADAS): aplicar **TODAS** las correcciones y mejoras, **incluido lo estructural
> (HK-1..HK-4)**; **NUNCA commitear ni pushear** (modo `/no-tocar-git`); la NC es **solo anulación total**; CM es la
> referencia y CP debe alcanzarla.

## Reglas permanentes (no romper en toda la noche)

1. **NO git**: nunca `git commit/push/add/reset/checkout/merge/rebase/stash`. Solo lectura (`status/diff/log`).
   El árbol queda modificado, coherente y compilando; el usuario revisa y commitea a la mañana.
2. **Build verde + tests verdes después de CADA lote.** Si un cambio rompe el build o un test, arreglalo antes de
   seguir; si no podés, revertí ese lote (a mano, sin git) y dejalo anotado como bloqueante. Nunca dejes el árbol roto.
3. **0 warnings** como objetivo. No introducir warnings nuevos.
4. **Respetar la arquitectura** (`doc/arquitectura/*`): Core sin deps, Services/Infra solo Core, sin lógica en
   code-behind, sin `MessageBox`/`Console.WriteLine`, servicios con `ServiceResult`, I/O async con `CancellationToken`.
5. **CM es la referencia.** Al homogeneizar, llevá CP al nivel de CM (salvo regla de negocio que lo justifique).
6. **Documentar cada decisión** en `doc/decisiones/registro-decisiones.md` y el avance en
   `doc/mejoras/auditoria-progreso.md` (este es el estado vivo: actualizalo al cerrar cada lote).
7. **No inventar resultados.** Si algo requiere red/credenciales/decisión de negocio no disponible, implementá lo
   posible y dejalo anotado como paso manual/bloqueante. No simular CAE ni respuestas de AFIP reales.
8. **Trabajá en lotes chicos y atómicos**, validando entre cada uno. Preferí quick wins primero.

## FASE 1 — Auditoría (solo lectura de código)

Ejecutá íntegramente el prompt de `doc/mejoras/prompt-auditoria-integral.md` (pasos 0–9 + hallazgos conocidos
HK-1..HK-4). Producí el informe completo en `doc/mejoras/informe-auditoria.md` con: resumen ejecutivo, tabla de
hallazgos priorizada, homogeneización CP↔CM, catálogo iconos/tooltips, paridad CM→CP, divergencias código↔doc,
plan de corrección por lotes, plan de documentación y plan de skills. Borradores de docs/skills nuevos en
`doc/mejoras/borradores/`. **No corrijas código en esta fase.**

Al terminar la Fase 1, escribí en `doc/mejoras/auditoria-progreso.md` la lista de lotes de corrección numerados
(L1, L2, …) en el orden de ataque, cada uno con su checklist. Esa lista guía la Fase 2.

## FASE 2 — Corrección y mejoras (aplicar TODO, sin commitear)

Recorré los lotes definidos por la Fase 1, en orden, marcando progreso. Aplicá **todo** lo encontrado, incluido lo
estructural. Orden recomendado (ajustable según el informe):

- **L-A Quick wins seguros:** usings sin uso, código muerto, `dotnet format`, strings/números mágicos a constantes,
  textos de UI inconsistentes, comentarios obsoletos. (Bajo riesgo, build/tests verdes.)
- **L-B Homogeneización CP↔CM (no estructural):** llevar CP al nivel de CM en ViewModels/Pages/servicios
  equivalentes, bases MVVM, converters, estilos, manejo de errores y logging. Paridad funcional CM→CP.
- **L-C Consistencia visual / pulido (ver sección "Pulido" abajo):** iconos + tooltips homogéneos en todos los
  botones de operación, estados de carga/vacío/error, espaciados, estilos centralizados, formato es-AR de
  importes/fechas, tema claro/oscuro coherente.
- **L-D Estructural — agregado Recibo con ítems (HK-1/HK-2/HK-3):** entidad de ítems del recibo + snapshot
  inmutable, config EF + índices, **migración CP+CM con backfill** de históricos, servicio de emisión, UI de N
  ítems en ambas apps, total derivado, PDF (`Afip.Documentos`/`PdfService`) y mapeo AFIP (WSFE con N ítems).
- **L-E Estructural — NC / anulación (HK-4):** `NotaDeCredito` con snapshot de ítems/detalle/importe copiado del
  recibo; **solo anulación total** (NC por el total, recibo→`Anulado`); transiciones de estado válidas; PDF y AFIP
  (`CbtesAsoc`). Paridad CP↔CM.
- **L-F Documentación canónica (Paso 9):** crear/actualizar `doc/` (diseño/negocio/implementación) y los skills
  nuevos para que los agentes no diverjan a futuro. Incluir el catálogo operación→icono→tooltip y la receta
  "agregar entidad end-to-end" + Definition of Done.
- **L-G Tests:** cubrir los huecos detectados (emisión con N ítems, snapshot, anulación total, backfill, paridad
  CP/CM). Todo verde.
- **L-Z Cierre:** correr `/validar-plataforma` completo (build, tests, migraciones, smoke runtime ambas apps,
  reglas estáticas). Dejar el resumen final en `doc/mejoras/auditoria-progreso.md`.

Tras cada lote: compilar, testear, actualizar `auditoria-progreso.md`. **Sin commits.**

## Pulido — que la app quede "linda y limpia"

- **Consistencia visual**: iconos homogéneos por operación (mismo icono/mismo tooltip en toda la solución y entre
  CP/CM), estilos de botón centralizados en `Styles.xaml` (nada inline duplicado), espaciados/paddings/alineaciones
  uniformes, jerarquía tipográfica consistente.
- **Estados de UI**: estado **vacío** (mensaje + icono cuando una lista no tiene datos), estado de **carga**
  (spinner/`IsBusy` consistente), estado de **error** claro vía `IDialogService`/snackbar. Nada de pantallas mudas.
- **Tema**: claro/oscuro coherentes; acentos por app correctos; ningún color hardcodeado (todo desde `Colors.xaml`/Fluent).
- **Formato regional**: importes y fechas con cultura es-AR consistente (separador de miles, símbolo $, decimales),
  en UI, PDF y mail.
- **Microdetalles**: títulos de ventana, icono de la app, tooltips en acciones, foco/hover/teclado, orden de tabulación,
  textos sin typos y en español consistente, columnas de grillas alineadas (números a la derecha).
- **Limpieza de código**: `dotnet format`, sin usings sin uso, sin código muerto, sin TODOs resueltos, sin warnings,
  XML-doc en APIs públicas clave, un archivo por clase, nombres consistentes (dominio español / técnico inglés).
- **README / docs** actualizados al estado real.

## Mecánica del loop (cómo continuar entre iteraciones)

1. Al iniciar cada iteración, leé `doc/mejoras/auditoria-progreso.md` para saber en qué lote vas.
2. Hacé el **siguiente** sub-lote pendiente (lo más chico que deje build+tests verdes).
3. Validá (build + tests; si tocaste modelo, `dotnet ef migrations has-pending-model-changes`).
4. Actualizá el progreso (qué quedó hecho, qué sigue, bloqueantes).
5. Repetí hasta completar L-Z. Cuando todo esté ✅ y validado, **terminá el loop** y dejá el resumen final.
