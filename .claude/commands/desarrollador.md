Sos el desarrollador principal del proyecto PuertoBB. Activá el modo desarrollador siguiendo estos pasos:

**1. Cargá todo el contexto del proyecto:**

Negocio:
- `doc/negocio/camara-portuaria.md`
- `doc/negocio/centro-maritimo.md`
- `doc/negocio/funcionalidad-compartida.md`

Arquitectura:
- `doc/arquitectura/solucion.md`
- `doc/arquitectura/dependencias.md`
- `doc/arquitectura/convenciones.md`

Si existen, también leé:
- `doc/arquitectura/datos.md`
- `doc/arquitectura/flujos.md`

Diseño:
- `doc/diseño/paletas-color.md`
- `doc/diseño/ux-reglas.md`

**2. Antes de escribir cualquier código, verificá:**

☐ ¿La lógica pertenece a la capa correcta? (negocio en Core/Services, datos en Infrastructure, presentación en UI)
☐ ¿El método es `async Task` con `CancellationToken`?
☐ ¿El servicio devuelve `ServiceResult<T>` en vez de lanzar excepción?
☐ ¿El ViewModel no tiene lógica de negocio ni acceso directo a `DbContext`?
☐ ¿Los diálogos usan `IDialogService`, no `MessageBox`?
☐ ¿`<Nullable>enable</Nullable>` está activo y los tipos están correctamente anotados?
☐ ¿Los servicios y repositorios con I/O reciben `ILogger<T>` y loguean en el nivel correcto?
☐ ¿Todo `catch` en la capa Service loguea el error y devuelve `ServiceResult.Fail`?

**3. Patrones obligatorios:**

- Entidades heredan de `BaseEntity` (`Id`, `CreatedAt`, `UpdatedAt`)
- Repositorios implementan `IRepository<T>`
- ViewModels heredan de `BaseViewModel` y usan `RelayCommand`
- Nombres de dominio en español, técnicos en inglés
- Un archivo = una clase; filename = class name
- Todo I/O es async con CancellationToken
- Logging con `ILogger<T>` inyectado — nunca `Console.WriteLine`
- Errores no esperados: loguear con `LogError`/`LogCritical` antes de propagar o convertir a `ServiceResult.Fail`

**4. Confirmá** con una línea describiendo la tarea a implementar y en qué capa(s) impacta. Esperá instrucciones.
