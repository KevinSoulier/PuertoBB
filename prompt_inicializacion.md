# Prompt de inicialización — PuertoBB

> **Documento histórico** (anterior a la decisión D-21, 2026-06-11): el feature "apoderado
> fiscal" descripto más abajo fue **eliminado** del producto. No usar este documento como
> referencia del diseño actual — la fuente de verdad es `doc/`.

Copiá todo el texto debajo de esta línea y pegalo como primer mensaje al agente.

---

Sos el asistente de desarrollo del proyecto **PuertoBB**. Tu primera tarea es inicializar la solución completa y luego quedarte con todas las convenciones y skills cargadas para el resto de la sesión.

## Tarea 1 — Inicializar la solución

Creá la siguiente estructura de solución .NET 10 con todos los proyectos, referencias y archivos base:

```
PuertoBB.sln
├── PuertoBB.Core              # Class library — modelos, interfaces, excepciones
├── PuertoBB.Services          # Class library — AFIP, PDF, Mail
├── PuertoBB.Infrastructure    # Class library — EF Core, SQLite, repositorios
├── CamaraPortuaria.UI         # WPF App — app Cámara Portuaria
└── CentroMaritimo.UI          # WPF App — app Centro Marítimo
```

### Pasos concretos a ejecutar

1. Crear la solución: `dotnet new sln -n PuertoBB`
2. Crear los proyectos:
   - `dotnet new classlib -n PuertoBB.Core -f net10.0`
   - `dotnet new classlib -n PuertoBB.Services -f net10.0`
   - `dotnet new classlib -n PuertoBB.Infrastructure -f net10.0`
   - `dotnet new wpf -n CamaraPortuaria.UI -f net10.0-windows`
   - `dotnet new wpf -n CentroMaritimo.UI -f net10.0-windows`
3. Agregar todos los proyectos a la solución
4. Establecer referencias entre proyectos:
   - `PuertoBB.Services` → `PuertoBB.Core`
   - `PuertoBB.Infrastructure` → `PuertoBB.Core`
   - `CamaraPortuaria.UI` → `PuertoBB.Core` + `PuertoBB.Services` + `PuertoBB.Infrastructure`
   - `CentroMaritimo.UI` → `PuertoBB.Core` + `PuertoBB.Services` + `PuertoBB.Infrastructure`
5. Instalar paquetes NuGet:
   - `PuertoBB.Infrastructure`: `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`
   - `PuertoBB.Services`: `QuestPDF`, `MailKit`
   - `CamaraPortuaria.UI` y `CentroMaritimo.UI`: `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Hosting`
6. Habilitar `<Nullable>enable</Nullable>` e `<ImplicitUsings>enable</ImplicitUsings>` en todos los `.csproj`
7. Crear `.gitignore` apropiado para .NET + VS Code + VS 2026
8. Crear estructura de carpetas vacías con `.gitkeep` en cada proyecto según las convenciones detalladas más abajo
9. Crear `README.md` en la raíz con descripción breve del proyecto

---

## Tarea 2 — Cargar skills y convenciones

Una vez inicializada la solución, internalizá todo lo siguiente. Esto aplica a **cada archivo que toques** durante esta sesión y las futuras.

---

### SKILL: Contexto de negocio

**Qué es PuertoBB:**
Sistema de gestión de recibos para dos entidades portuarias de Bahía Blanca. La usuaria es Laura, que administra ambas entidades de forma unipersonal desde la misma oficina.

**Cámara Portuaria** emite recibos a **empresas**:
- ~29 empresas con cuota social mensual (mismo importe para todas)
- Grupos extraordinarios (~5 empresas con importe distinto)
- Cobros extraordinarios puntuales posibles (ej. papelería)
- Los grupos pueden cambiar: agregar empresas, modificar integrantes

**Centro Marítimo** emite recibos a **agencias**:
- ~13 agencias con cuota social mensual (mismo importe para todas)
- Vouchers: cada voucher representa un barco que ingresó al puerto gestionado por esa agencia
- Pueden existir múltiples vouchers de la misma agencia en el mismo mes
- Al cerrar el período los vouchers de una agencia se consolidan en un único recibo con leyenda de números de voucher y total
- Numeración de vouchers por serie con control del mayor número usado
- Posibilidad de facturar como persona apoderada (el emisor fiscal es el apoderado, el documento identifica al Centro Marítimo)
- Cobros extraordinarios independientes posibles

**Funcionalidad compartida entre ambas entidades:**
- ABM de empresas/agencias con grupos de facturación
- Emisión masiva de recibos por período y grupo (bloqueo de duplicados)
- Emisión individual de recibos fuera del ciclo masivo
- Integración con AFIP/ARCA (webservice WSFE) para obtener CAE
- Notas de crédito para anular recibos
- Estados de recibo: Emitido / Enviado / Pagado / Vencido / Anulado
- Envío automático de PDF por mail al emitir
- Dashboard de pendientes con alertas configurables
- Backup manual del archivo SQLite

---

### SKILL: Stack tecnológico

| Componente | Tecnología |
|---|---|
| Runtime | .NET 10 (LTS) |
| UI | WPF + MVVM estricto |
| Base de datos | SQLite embebida (un archivo por entidad) |
| ORM | Entity Framework Core 10 |
| PDF | QuestPDF |
| Email | MailKit |
| Integración fiscal | AFIP/ARCA — WSFE (SOAP) |
| DI | Microsoft.Extensions.DependencyInjection |
| IDE principal | VS Code + extensiones C# |
| IDE secundario | Visual Studio 2026 (solo WPF designer) |
| Control de versiones | Git + GitHub |

---

### SKILL: Arquitectura y estructura de carpetas

**Regla de dependencias — solo en esta dirección:**
```
UI → Core ← Services
UI → Core ← Infrastructure
```
Core nunca depende de nada más.

**PuertoBB.Core:**
```
PuertoBB.Core/
├── Entities/
│   ├── Common/         # Recibo, Empresa, Grupo, ConfiguracionEntidad, etc.
│   └── CentroMaritimo/ # Voucher, Apoderado
├── Enums/              # ReciboEstado, TipoComprobante, TipoEntidad
├── Interfaces/
│   ├── Repositories/   # IRepository<T>, IEmpresaRepository, IReciboRepository, etc.
│   └── Services/       # IAfipService, IPdfService, IMailService, IDialogService
└── Exceptions/         # AfipException, ReciboException, etc.
```

**PuertoBB.Services:**
```
PuertoBB.Services/
├── Afip/
│   ├── AfipService.cs
│   ├── AfipAuthService.cs
│   └── Models/
├── Pdf/
│   └── RecibosPdfService.cs
└── Mail/
    └── MailService.cs
```

**PuertoBB.Infrastructure:**
```
PuertoBB.Infrastructure/
├── Data/
│   ├── CamaraPortuariaDbContext.cs
│   ├── CentroMaritimoDbContext.cs
│   └── Configurations/
├── Repositories/
└── Migrations/
    ├── CamaraPortuaria/
    └── CentroMaritimo/
```

**CamaraPortuaria.UI / CentroMaritimo.UI:**
```
<App>.UI/
├── App.xaml / App.xaml.cs
├── Views/
│   ├── MainWindow.xaml
│   ├── Empresas/        (o Agencias/ para CentroMaritimo)
│   ├── Recibos/
│   ├── Vouchers/        (solo CentroMaritimo)
│   └── Configuracion/
├── ViewModels/
│   ├── Base/
│   │   ├── BaseViewModel.cs
│   │   └── RelayCommand.cs
│   ├── Empresas/
│   └── Recibos/
├── Converters/
└── Resources/
    ├── Styles.xaml
    └── Colors.xaml
```

---

### SKILL: Convenciones de código

**Entidades — heredan de BaseEntity:**
```csharp
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

**Interfaces de repositorio:**
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

**Resultados de servicios — sin excepciones para flujo de negocio:**
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

**MVVM — BaseViewModel:**
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

**Reglas que nunca se rompen:**
- Cero lógica de negocio en code-behind. Si hay algo más que `InitializeComponent()`, está mal.
- Toda operación de I/O es `async Task` con `CancellationToken`.
- `<Nullable>enable</Nullable>` activo en todos los proyectos.
- Nombres de dominio en español (`Empresa`, `Recibo`, `Grupo`). Código técnico en inglés (`Repository`, `ViewModel`, `DbContext`).
- Un archivo por clase. Nombre del archivo = nombre de la clase.
- Nunca `MessageBox` directo — siempre a través de `IDialogService` inyectado.

---

### SKILL: Diseño WPF

**Paleta — Cámara Portuaria:**
```xml
AccentColor:      #1565C0  (azul marino)
AccentLightColor: #E3F2FD
```

**Paleta — Centro Marítimo:**
```xml
AccentColor:      #00695C  (verde azulado)
AccentLightColor: #E0F2F1
```

**Neutrales compartidos:**
```xml
BackgroundColor:     #F5F5F5
SurfaceColor:        #FFFFFF
BorderColor:         #E0E0E0
TextPrimaryColor:    #1A1A1A
TextSecondaryColor:  #6B6B6B
SuccessColor:        #2E7D32
WarningColor:        #F57C00
ErrorColor:          #C62828
```

**Layout general:** sidebar de navegación de 220px a la izquierda (color acento), área de contenido principal a la derecha con `ContentControl` o `Frame`.

**Márgenes:** 24px exterior de página, 16px entre secciones, 8px entre elementos relacionados.

**Reglas de UX:**
- Validación en tiempo real con mensajes bajo el campo, nunca en popup.
- Estados de carga con overlay `ProgressBar` o `ProgressRing`, sin bloquear la UI.
- Mensajes de error/éxito en barra superior de la vista, no en dialogs.
- Confirmaciones destructivas (eliminar, anular) siempre en dialog modal.
- Fuente: Segoe UI. Sin fuentes externas.
- Sin `MessageBox` nativo — siempre `IDialogService`.

**Colores de estado de recibo:**
```
Emitido  → fondo #E3F2FD (azul claro)
Enviado  → fondo #FFF9C4 (amarillo claro)
Pagado   → fondo #E8F5E9 (verde claro)
Vencido  → fondo #FFEBEE (rojo claro)
Anulado  → fondo #F5F5F5 (gris claro)
```

---

## Confirmación esperada del agente

Al terminar, el agente debe responder con:
1. Confirmación de que la solución fue inicializada correctamente
2. Árbol de archivos creados
3. Confirmación de que las skills fueron internalizadas
4. Primer paso sugerido para continuar el desarrollo

