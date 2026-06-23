using System.Windows.Input;
using CamaraPortuaria.UI.Services;
using PuertoBB.Core.Models.Resultados;

namespace CamaraPortuaria.UI.ViewModels.Base;

/// <summary>Base para ViewModels de página: estado de carga (IsBusy + progreso) y toasts Fluent.</summary>
public abstract class PageViewModel : BaseViewModel
{
    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set => SetField(ref _isBusy, value); }

    // ── Estado del overlay de espera (lo consume el control BusyOverlay) ────────────────────────
    private string _busyTitulo = "Procesando…";
    /// <summary>Verbo de la operación en curso (ej. "Emitiendo y enviando").</summary>
    public string BusyTitulo { get => _busyTitulo; set => SetField(ref _busyTitulo, value); }

    private string _busyDetalle = string.Empty;
    /// <summary>Cliente que se está procesando ahora (ej. "Acme S.A.").</summary>
    public string BusyDetalle { get => _busyDetalle; set => SetField(ref _busyDetalle, value); }

    private int _busyActual;
    public int BusyActual
    {
        get => _busyActual;
        set { if (SetField(ref _busyActual, value)) OnPropertyChanged(nameof(BusyContador)); }
    }

    private int _busyTotal;
    public int BusyTotal
    {
        get => _busyTotal;
        set { if (SetField(ref _busyTotal, value)) { OnPropertyChanged(nameof(BusyContador)); OnPropertyChanged(nameof(BusyMostrarProgreso)); } }
    }

    /// <summary>True cuando hay un total conocido → se muestra la barra determinada y el contador.</summary>
    public bool BusyMostrarProgreso => _busyTotal > 0;

    /// <summary>Contador "N / M" (vacío si no hay total).</summary>
    public string BusyContador => _busyTotal > 0 ? $"{_busyActual} / {_busyTotal}" : string.Empty;

    private ICommand? _busyCancelCommand;
    public ICommand? BusyCancelCommand
    {
        get => _busyCancelCommand;
        set { if (SetField(ref _busyCancelCommand, value)) OnPropertyChanged(nameof(BusyCancelable)); }
    }

    /// <summary>True cuando la operación en curso admite cancelación (muestra el botón Cancelar).</summary>
    public bool BusyCancelable => _busyCancelCommand is not null;

    // ── Helpers para ejecutar operaciones con el overlay ────────────────────────────────────────

    /// <summary>Ejecuta una operación corta/de un paso: spinner indeterminado, sin cancelar ni contador.</summary>
    protected async Task EjecutarOcupadoAsync(string titulo, Func<Task> operacion)
    {
        ReiniciarBusy(titulo);
        IsBusy = true;
        try { await operacion(); }
        finally { IsBusy = false; BusyCancelCommand = null; }
    }

    /// <summary>
    /// Ejecuta una operación masiva con progreso por ítem y botón Cancelar. La <paramref name="operacion"/>
    /// recibe el <see cref="IProgress{T}"/> y el token (para pasarlos al servicio o a un loop propio).
    /// Si se cancela, muestra el toast "Operación cancelada".
    /// </summary>
    protected async Task EjecutarConProgresoAsync(
        string titulo, Func<IProgress<ProgresoMasivo>, CancellationToken, Task> operacion)
    {
        using var cts = new CancellationTokenSource();
        ReiniciarBusy(titulo);
        BusyDetalle = "Preparando…";
        BusyCancelCommand = new RelayCommand(_ => cts.Cancel());
        IsBusy = true;
        var progreso = new Progress<ProgresoMasivo>(p =>
        {
            BusyTotal = p.Total;
            BusyActual = p.Actual;
            BusyDetalle = p.Cliente;
        });
        try { await operacion(progreso, cts.Token); }
        catch (OperationCanceledException) { MostrarAdvertencia("Operación cancelada."); }
        finally { IsBusy = false; BusyCancelCommand = null; }
    }

    /// <summary>Variante que devuelve el resultado de la operación, o null si se canceló.</summary>
    protected async Task<T?> EjecutarConProgresoAsync<T>(
        string titulo, Func<IProgress<ProgresoMasivo>, CancellationToken, Task<T>> operacion) where T : class
    {
        T? resultado = null;
        await EjecutarConProgresoAsync(titulo, async (progreso, ct) => { resultado = await operacion(progreso, ct); });
        return resultado;
    }

    private void ReiniciarBusy(string titulo)
    {
        BusyTitulo = titulo;
        BusyDetalle = string.Empty;
        BusyActual = 0;
        BusyTotal = 0;
        BusyCancelCommand = null;
    }

    /// <summary>Dispara la carga inicial de la página (fire-and-forget) capturando errores: si la carga
    /// falla (BD inaccesible, servicio caído) muestra un toast en vez de dejar la página en blanco en silencio.</summary>
    protected void CargarSeguro(Func<Task> carga) => _ = CargarSeguroAsync(carga);

    private async Task CargarSeguroAsync(Func<Task> carga)
    {
        try { await carga(); }
        catch (Exception ex) { MostrarError($"No se pudieron cargar los datos: {ex.Message}"); }
    }

    protected void MostrarError(string mensaje) => SnackbarHost.Error(mensaje);
    protected void MostrarAdvertencia(string mensaje) => SnackbarHost.Advertencia(mensaje);
    protected void MostrarExito(string mensaje) => SnackbarHost.Exito(mensaje);
    protected void LimpiarStatus() { /* obsoleto: el toaster se autocierra por timeout */ }
}
