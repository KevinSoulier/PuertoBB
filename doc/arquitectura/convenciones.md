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

## Reglas que nunca se rompen

- **Cero lógica de negocio en code-behind.** Si hay algo más que `InitializeComponent()`, está mal.
- **Toda operación de I/O es `async Task` con `CancellationToken`.**
- **`<Nullable>enable</Nullable>` activo en todos los proyectos.**
- **Nombres de dominio en español** (`Empresa`, `Recibo`, `Grupo`). **Código técnico en inglés** (`Repository`, `ViewModel`, `DbContext`).
- **Un archivo por clase.** Nombre del archivo = nombre de la clase.
- **Nunca `MessageBox` directo** — siempre a través de `IDialogService` inyectado.
- **`ServiceResult<T>`** para resultados de servicios, no excepciones para flujo de negocio.
- Las excepciones (`AfipException`, `ReciboException`) son solo para errores técnicos inesperados.

## Idiomas

| Contexto | Idioma |
| --- | --- |
| Nombres de entidades de dominio | Español |
| Nombres técnicos (patrones, infraestructura) | Inglés |
| Comentarios | Español |
| Mensajes de UI | Español |
