# Receta — Agregar una entidad/feature end-to-end

> "Cómo se hace acá." Seguir estos pasos en orden para sumar una entidad o un CRUD nuevo sin romper la
> arquitectura ni la paridad CP↔CM. Cada paso deja **build verde + tests verdes**.
> Reglas base: `doc/arquitectura/convenciones.md`, `dependencias.md`; UI: `doc/diseño/design-system.md`.

## Antes de empezar

- ¿La entidad es de **CamaraPortuaria**, de **CentroMaritimo**, o de ambos dominios? Si aplica a los dos, se crea
  **una versión por dominio** (namespaces `...Entities.CamaraPortuaria` / `...Entities.CentroMaritimo`) y se mantiene
  **paridad**: misma forma salvo lo que el negocio justifique.
- **CM es la referencia**: si ya existe algo equivalente en CM, copiá ese patrón a CP.

## Pasos

### 1. Core — entidad + contrato
- `PuertoBB.Core/Entities/<Dominio>/<Entidad>.cs` heredando de `BaseEntity` (Id/CreatedAt/UpdatedAt).
- Nombres de dominio en **español**; nullability correcta; sin dependencias externas.
- Si necesita acceso a datos: interfaz `IRepository<T>`-based en `Core/Interfaces/Repositories/<Dominio>/I<Entidad>Repository.cs`.
- Si necesita lógica de negocio: método en la interfaz de servicio correspondiente, devolviendo `ServiceResult<T>`.

### 2. Infrastructure — EF + migración
- `Data/Configurations/<Dominio>/<Entidad>Configuration.cs` (`IEntityTypeConfiguration<>`): índices únicos, `HasMaxLength` en strings, FKs con índice, `DeleteBehavior` explícito.
- Registrar el `DbSet<>` en el `DbContext` del dominio y aplicar la configuration.
- Repo concreto en `Repositories/<Dominio>/<Entidad>Repository.cs` (heredar `RepositoryBase<>`; lecturas con `AsNoTracking` + `Include` explícito; `CancellationToken` siempre).
- Migración: `dotnet ef migrations add <Nombre>` para **CP y CM** (los dos contextos). Verificar con
  `dotnet ef migrations has-pending-model-changes`. Si hay datos, prever **backfill**.

### 3. Services — lógica de negocio
- Implementar en `PuertoBB.Services/Negocio/...` devolviendo `ServiceResult<T>`.
- **Todo `try/catch` de I/O (AFIP/mail/PDF/DB) vive acá**, nunca en el ViewModel.
- Logging `ILogger<T>` estructurado (`{Propiedad}`), niveles correctos (Info/Warning/Error). Nada de `Console.WriteLine`.
- Validaciones y guardas de estado (ej. transiciones de `ReciboEstado`) en el service.

### 4. DI — registro
- Registrar repo y service en `DependencyInjection.cs` (Infrastructure/Services) y/o en `App.xaml.cs` de cada UI.
- Lifetimes coherentes con los existentes.

### 5. UI — ViewModel + Page (ambas apps)
- ViewModel en `ViewModels/` heredando `BaseViewModel`/`PageViewModel`; comandos `AsyncRelayCommand`/`RelayCommand`.
- **Sin `try/catch`, sin `DbContext`, sin lógica en code-behind** (solo `InitializeComponent`). Evaluar `result.Success` y mostrar `result.ErrorMessage` vía `IDialogService`.
- Page XAML siguiendo el **patrón de página** del design system: título, barra de acciones con `AccionIconButton` (icono + **ToolTip** del catálogo), grid, estados vacío/carga/error.
- Aplicar en **CP y CM** con el mismo layout (solo cambia el acento).

### 6. Tests
- xUnit + NSubstitute + SQLite in-memory. Cubrir: caminos OK y de error (`Fail`), validaciones, paridad CP/CM del flujo.

### 7. Docs
- Si introduce una decisión, anotarla en `doc/decisiones/registro-decisiones.md`.
- Si toca negocio, actualizar `doc/negocio/...`.

## Definition of Done (checklist)

- [ ] `dotnet build PuertoBB.slnx` → **0 errores / 0 warnings**.
- [ ] `dotnet test` → todo verde (incluye tests nuevos del flujo).
- [ ] `dotnet ef migrations has-pending-model-changes` → sin cambios pendientes (CP y CM).
- [ ] Paridad CP↔CM verificada (misma estructura/acciones/textos; solo difiere lo justificado por negocio).
- [ ] UI: estilos del design system, **iconos del catálogo**, **ToolTips** en botones de operación, sin colores hardcodeados, sin `MessageBox`.
- [ ] Sin `try/catch` en ViewModels, sin `Console/Debug.WriteLine`, servicios devuelven `ServiceResult<T>`.
- [ ] Decisiones/negocio documentados si corresponde.
