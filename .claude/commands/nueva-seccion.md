Sos el desarrollador de PuertoBB y vas a agregar una **entidad/sección nueva end-to-end** siguiendo el estándar del proyecto, sin improvisar ni divergir de lo ya hecho.

## Antes de tocar código, cargá el estándar (obligatorio)

Leé y seguí como fuente de verdad:
- `doc/arquitectura/receta-entidad-end-to-end.md` — los pasos exactos (Core→EF/migración→Services→DI→UI→Tests→Docs) y la **Definition of Done**.
- `doc/arquitectura/convenciones.md` y `doc/arquitectura/dependencias.md` — reglas que no se rompen.
- `doc/diseño/design-system.md` — patrón de página y **catálogo canónico operación→icono→tooltip** (misma operación = mismo icono + ToolTip en toda la solución).
- Si es de negocio: `doc/negocio/...`. Si toca AFIP: `doc/arquitectura/afip-integracion.md`.

## Cómo trabajar

1. Confirmá si la sección es de **CamaraPortuaria**, **CentroMaritimo** o **ambas**. Si es de ambas, implementá las dos manteniendo **paridad** (CM es la referencia; CP debe igualarla, salvo diferencia justificada por negocio).
2. Buscá una sección equivalente ya existente y **copiá su patrón** (entidad, configuration EF, repo, service, ViewModel, Page) en vez de inventar uno nuevo.
3. Avanzá en **lotes chicos**, compilando y testeando entre cada uno. Nunca dejes el árbol roto.
4. UI: estilo `AccionIconButton`, iconos y ToolTips del catálogo, sin colores hardcodeados, sin `MessageBox` (usar `IDialogService`), sin lógica en code-behind, ViewModels sin `try/catch` ni `DbContext`.
5. Migraciones: `dotnet ef migrations add <Nombre>` para **CP y CM**; verificá `has-pending-model-changes`. Si hay datos, definí backfill.

## Cierre (Definition of Done)

Antes de dar por terminado, verificá el checklist de la receta:
- `dotnet build PuertoBB.slnx` → 0 errores / 0 warnings.
- `dotnet test` → todo verde (con tests nuevos del flujo, caminos OK y de error).
- Migraciones en sync (CP y CM).
- Paridad CP↔CM; iconos+ToolTips del catálogo; servicios devuelven `ServiceResult<T>`.
- Decisión nueva documentada en `doc/decisiones/registro-decisiones.md` si corresponde.

**No commitees ni pushees** salvo que el usuario lo pida explícitamente.
