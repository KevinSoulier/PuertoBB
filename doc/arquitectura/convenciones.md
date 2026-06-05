# Convenciones de código

## Entidades — heredan de BaseEntity

```csharp
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

## Interfaces de repositorio

```csharp
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
```

## Resultados de servicios — sin excepciones para flujo de negocio

```csharp
public record ServiceResult<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? ErrorMessage { get; init; }

    public static ServiceResult<T> Ok(T data) => new() { Success = true, Data = data };
    public static ServiceResult<T> Fail(string error) => new() { Success = false, ErrorMessage = error };
}
```

## MVVM — BaseViewModel

```csharp
public abstract class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
```

## Logging — `ILogger<T>`

Todos los servicios, repositorios y ViewModels que hacen I/O reciben `ILogger<T>` por DI.  
**Nunca** usar `Console.WriteLine`, `Debug.WriteLine` ni archivos manuales.

### Qué loguear por nivel

| Nivel | Cuándo |
|---|---|
| `LogDebug` | Detalles internos útiles durante desarrollo (parámetros de llamadas, queries) |
| `LogInformation` | Eventos de negocio normales: recibo emitido, mail enviado, período cerrado |
| `LogWarning` | Situaciones recuperables: mail falló (recibo queda Emitido), duplicado bloqueado, AFIP retry |
| `LogError` | Errores no esperados que afectan una operación: AFIP rechazó CAE, fallo de DB |
| `LogCritical` | Excepciones no manejadas que llegan al handler global |

### Proveedor: Serilog con sink de archivo

Configurado en `App.xaml.cs` antes de construir el host. Archivos diarios en `%AppData%\Local\PuertoBB\{App}\Logs\`.

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(
        path: Path.Combine(logPath, "app-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .Enrich.FromLogContext()
    .CreateLogger();

hostBuilder.UseSerilog();
```

### Ejemplo de uso en servicio

```csharp
_logger.LogInformation("Recibo emitido: Empresa={EmpresaId} Período={Anio}/{Mes} CAE={CAE}",
    recibo.EmpresaId, recibo.PeriodoAnio, recibo.PeriodoMes, recibo.CAE);

_logger.LogWarning("Envío de mail falló para Empresa={EmpresaId}: {Error}",
    empresa.Id, ex.Message);

_logger.LogError(ex, "Error al solicitar CAE para Empresa={EmpresaId}", empresa.Id);
```

Usar **logging estructurado** (propiedades nombradas `{Nombre}`, no interpolación de strings).

---

## Manejo de errores

### Capas y responsabilidades

```
UI (ViewModel)
  └─ Recibe ServiceResult; nunca expone excepciones al usuario
       └─ Service
            ├─ Errores de negocio → ServiceResult.Fail(mensaje)
            ├─ AfipException / MailException → captura, loguea → ServiceResult.Fail
            └─ Repository
                 ├─ Operaciones normales → propaga excepciones al Service
                 └─ DbUpdateException → loguea + relanza como ReciboException
```

### Reglas

- **Services:** siempre devuelven `ServiceResult<T>`. Nunca propagan excepciones al ViewModel.  
  Todo `try/catch` de I/O externo (AFIP, mail, PDF) se hace en la capa Service.
- **Repositories:** pueden lanzar excepciones; envuelven `DbUpdateException` en `ReciboException` con mensaje legible.
- **ViewModels:** nunca tienen `try/catch`. Evalúan `result.Success` y muestran `result.ErrorMessage` en la UI.

### Handler global de excepciones no manejadas

Configurado en `App.xaml.cs`. El sistema **nunca crashea sin aviso**.

```csharp
// Excepciones en el hilo de UI
DispatcherUnhandledException += (s, e) =>
{
    _logger.LogCritical(e.Exception, "Excepción no manejada en hilo UI");
    ShowCriticalErrorDialog(e.Exception.Message);
    e.Handled = true; // evita cierre abrupto
};

// Excepciones en hilos background
AppDomain.CurrentDomain.UnhandledException += (s, e) =>
{
    var ex = e.ExceptionObject as Exception;
    _logger.LogCritical(ex, "Excepción no manejada en hilo background");
    Log.CloseAndFlush(); // garantiza que los logs se persistan antes de salir
};

// Tasks sin await con excepción no observada
TaskScheduler.UnobservedTaskException += (s, e) =>
{
    _logger.LogError(e.Exception, "Task exception no observada");
    e.SetObserved(); // evita que el proceso termine
};
```

`ShowCriticalErrorDialog` muestra un dialog vía `IDialogService` con el mensaje de error y la ruta del archivo de log.

---

## Reglas que nunca se rompen

- **Cero lógica de negocio en code-behind.** Si hay algo más que `InitializeComponent()`, está mal.
- **Toda operación de I/O es `async Task` con `CancellationToken`.**
- **`<Nullable>enable</Nullable>` activo en todos los proyectos.**
- **Nombres de dominio en español** (`Empresa`, `Recibo`, `Grupo`). **Código técnico en inglés** (`Repository`, `ViewModel`, `DbContext`).
- **Un archivo por clase.** Nombre del archivo = nombre de la clase.
- **Nunca `MessageBox` directo** — siempre a través de `IDialogService` inyectado.
- **`ServiceResult<T>`** para resultados de servicios, no excepciones para flujo de negocio.
- Las excepciones (`AfipException`, `ReciboException`) son solo para errores técnicos inesperados.
- **Nunca `Console.WriteLine` ni `Debug.WriteLine`.** Siempre `ILogger<T>` inyectado.
- **El sistema nunca falla sin aviso.** Todo crash potencial tiene handler global que loguea + muestra dialog.

## Idiomas

| Contexto | Idioma |
| --- | --- |
| Nombres de entidades de dominio | Español |
| Nombres técnicos (patrones, infraestructura) | Inglés |
| Comentarios | Español |
| Mensajes de UI | Español |
