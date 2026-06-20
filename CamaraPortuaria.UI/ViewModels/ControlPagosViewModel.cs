using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using CamaraPortuaria.UI.ViewModels.Base;
using CamaraPortuaria.UI.ViewModels.Items;
using PuertoBB.Core.Common;
using PuertoBB.Core.Enums;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Resultados;

namespace CamaraPortuaria.UI.ViewModels;

public class ControlPagosViewModel : PageViewModel
{
    private readonly ICamaraPortuariaReciboService _recibos;
    private readonly IDialogService _dialog;

    // Debounce del buscador: evita pegarle a la base en cada tecla.
    private readonly DispatcherTimer _debounce;

    // Coalescencia de cargas: si llega una recarga mientras otra está en curso, se reejecuta al final
    // (en vez de cancelar con excepciones o solaparse). El dispatcher es single-thread, así que alcanza.
    private bool _cargando;
    private bool _recargaPendiente;

    public ObservableCollection<ReciboItem> Recibos { get; private set; } = [];

    private bool _soloVencidos;
    public bool SoloVencidos { get => _soloVencidos; set { if (SetField(ref _soloVencidos, value)) ResetYRecargar(); } }

    private bool _incluirIncobrables;
    public bool IncluirIncobrables { get => _incluirIncobrables; set { if (SetField(ref _incluirIncobrables, value)) ResetYRecargar(); } }

    private string _textoBusqueda = string.Empty;
    public string TextoBusqueda
    {
        get => _textoBusqueda;
        set { if (SetField(ref _textoBusqueda, value)) { _debounce.Stop(); _debounce.Start(); } }
    }

    public IReadOnlyList<string> EstadosFiltro { get; } =
        ["Pendientes de pago", "Emitido", "Vencido", "Pagado", "Incobrable", "Anulado", "Todos"];

    private string _filtroEstado = "Pendientes de pago";
    public string FiltroEstado { get => _filtroEstado; set { if (SetField(ref _filtroEstado, value)) ResetYRecargar(); } }

    // ── Paginado ────────────────────────────────────────────────────────────────────────────────
    public IReadOnlyList<int> TamaniosPagina { get; } = [50, 100, 200];

    private int _tamanioPagina = 100;
    public int TamanioPagina { get => _tamanioPagina; set { if (SetField(ref _tamanioPagina, value)) ResetYRecargar(); } }

    private int _paginaActual = 1;
    public int PaginaActual { get => _paginaActual; private set => SetField(ref _paginaActual, value); }

    private int _totalPaginas = 1;
    public int TotalPaginas { get => _totalPaginas; private set => SetField(ref _totalPaginas, value); }

    private int _totalRegistros;
    public int TotalRegistros { get => _totalRegistros; private set => SetField(ref _totalRegistros, value); }

    private ReciboItem? _seleccionado;
    public ReciboItem? Seleccionado { get => _seleccionado; set => SetField(ref _seleccionado, value); }

    /// <summary>Filas multiseleccionadas en la grilla (la setea el code-behind en SelectionChanged).</summary>
    public IReadOnlyList<ReciboItem> Seleccionados { get; set; } = [];

    private string _resumen = string.Empty;
    public string Resumen { get => _resumen; set => SetField(ref _resumen, value); }

    public ICommand BuscarCommand { get; }
    public ICommand PrimeraCommand { get; }
    public ICommand AnteriorCommand { get; }
    public ICommand SiguienteCommand { get; }
    public ICommand UltimaCommand { get; }
    public ICommand MarcarPagadoCommand { get; }
    public ICommand ReenviarCommand { get; }
    public ICommand MarcarIncobrableCommand { get; }
    public ICommand QuitarIncobrableCommand { get; }

    public ControlPagosViewModel(ICamaraPortuariaReciboService recibos, IDialogService dialog)
    {
        _recibos = recibos;
        _dialog = dialog;

        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); ResetYRecargar(); };

        BuscarCommand = new AsyncRelayCommand(CargarPaginaAsync);
        PrimeraCommand = new AsyncRelayCommand(() => IrAPaginaAsync(1), () => PaginaActual > 1);
        AnteriorCommand = new AsyncRelayCommand(() => IrAPaginaAsync(PaginaActual - 1), () => PaginaActual > 1);
        SiguienteCommand = new AsyncRelayCommand(() => IrAPaginaAsync(PaginaActual + 1), () => PaginaActual < TotalPaginas);
        UltimaCommand = new AsyncRelayCommand(() => IrAPaginaAsync(TotalPaginas), () => PaginaActual < TotalPaginas);
        MarcarPagadoCommand = new AsyncRelayCommand(MarcarPagadoAsync, () => Seleccionados.Any(x => x.EsPagable));
        ReenviarCommand = new AsyncRelayCommand(ReenviarAsync, () => Seleccionado?.EsReenviable == true);
        MarcarIncobrableCommand = new AsyncRelayCommand(MarcarIncobrableAsync, () => Seleccionados.Any(x => x.EsMarcableIncobrable));
        QuitarIncobrableCommand = new AsyncRelayCommand(QuitarIncobrableAsync, () => Seleccionados.Any(x => x.EsQuitableIncobrable));

        CargarSeguro(CargarPaginaAsync);
    }

    /// <summary>Cualquier cambio de filtro vuelve a la página 1 y recarga (con manejo de errores).</summary>
    private void ResetYRecargar() { PaginaActual = 1; CargarSeguro(CargarPaginaAsync); }

    private Task IrAPaginaAsync(int pagina)
    {
        PaginaActual = Math.Max(1, pagina); // el repo recorta al rango real y devuelve la página efectiva
        return CargarPaginaAsync();
    }

    private async Task CargarPaginaAsync()
    {
        if (_cargando) { _recargaPendiente = true; return; }
        _cargando = true;
        try
        {
            do
            {
                _recargaPendiente = false;
                await EjecutarOcupadoAsync("Actualizando", CargarInternoAsync);
            } while (_recargaPendiente);
        }
        catch (Exception ex) { MostrarError($"No se pudieron cargar los datos: {ex.Message}"); }
        finally { _cargando = false; }
    }

    private async Task CargarInternoAsync()
    {
        var filtro = new FiltroControlPagos
        {
            Estado             = MapEstado(_filtroEstado),
            SoloVencidos       = _soloVencidos,
            IncluirIncobrables = _incluirIncobrables,
            Texto              = string.IsNullOrWhiteSpace(_textoBusqueda) ? null : _textoBusqueda.Trim(),
            Pagina             = PaginaActual,
            TamanioPagina      = TamanioPagina,
        };
        var res = await _recibos.GetControlPaginadoAsync(filtro);
        if (!res.Success || res.Data is not { } page)
        {
            MostrarError(res.ErrorMessage ?? "No se pudieron cargar los pagos.");
            return;
        }

        Recibos = new ObservableCollection<ReciboItem>(page.Items.Select(r => new ReciboItem(r)));
        OnPropertyChanged(nameof(Recibos));
        Seleccionados = [];
        PaginaActual = page.Pagina;
        TotalPaginas = page.TotalPaginas;
        TotalRegistros = page.Total;
        Resumen = $"{page.Total} recibo(s) · {page.Vencidos} vencido(s)";
        CommandManager.InvalidateRequerySuggested();
    }

    private static FiltroEstadoControl MapEstado(string etiqueta) => etiqueta switch
    {
        "Emitido"    => FiltroEstadoControl.Emitido,
        "Vencido"    => FiltroEstadoControl.Vencido,
        "Pagado"     => FiltroEstadoControl.Pagado,
        "Incobrable" => FiltroEstadoControl.Incobrable,
        "Anulado"    => FiltroEstadoControl.Anulado,
        "Todos"      => FiltroEstadoControl.Todos,
        _            => FiltroEstadoControl.PendientesDePago,
    };

    private async Task MarcarPagadoAsync()
    {
        var objetivos = Seleccionados.Where(x => x.EsPagable).ToList();
        if (objetivos.Count == 0) return;
        if (!await _dialog.ShowConfirmAsync("Marcar como pagado",
                objetivos.Count == 1
                    ? $"¿Marcar el recibo {objetivos[0].Comprobante} de {objetivos[0].Empresa} como pagado?"
                    : $"¿Marcar {objetivos.Count} recibos como pagados?")) return;
        await EjecutarEnLoteAsync(objetivos, x => _recibos.MarcarPagadoAsync(x.Id),
            "Recibo marcado como pagado.", "recibo(s) marcados como pagados.");
    }

    private async Task ReenviarAsync()
    {
        if (Seleccionado is not { } sel) return;
        await EjecutarOcupadoAsync("Enviando", async () =>
        {
            var res = await _recibos.ReenviarMailAsync(sel.Id);
            if (res.Success) { MostrarExito("Recibo reenviado."); await CargarPaginaAsync(); }
            else MostrarError(res.ErrorMessage ?? "No se pudo reenviar.");
        });
    }

    private async Task MarcarIncobrableAsync()
    {
        var objetivos = Seleccionados.Where(x => x.EsMarcableIncobrable).ToList();
        if (objetivos.Count == 0) return;
        var descripcion = objetivos.Count == 1
            ? $"Se dará de baja la deuda del recibo {objetivos[0].Comprobante} de {objetivos[0].Empresa}."
            : $"Se dará de baja la deuda de {objetivos.Count} recibos.";
        var motivo = await _dialog.ShowInputAsync("Marcar incobrable", "Motivo (opcional)", null, descripcion);
        if (motivo is null) return; // cancelado
        await EjecutarEnLoteAsync(objetivos, x => _recibos.MarcarIncobrableAsync(x.Id, motivo),
            "Recibo marcado como incobrable.", "recibo(s) marcados como incobrables.");
    }

    private async Task QuitarIncobrableAsync()
    {
        var objetivos = Seleccionados.Where(x => x.EsQuitableIncobrable).ToList();
        if (objetivos.Count == 0) return;
        if (!await _dialog.ShowConfirmAsync("Quitar incobrable",
                objetivos.Count == 1
                    ? $"¿Reactivar la deuda del recibo {objetivos[0].Comprobante} de {objetivos[0].Empresa}?"
                    : $"¿Reactivar la deuda de {objetivos.Count} recibos?")) return;
        await EjecutarEnLoteAsync(objetivos, x => _recibos.QuitarIncobrableAsync(x.Id),
            "Baja por incobrable revertida.", "recibo(s) reactivados.");
    }

    /// <summary>Aplica una acción a cada recibo (con progreso y cancelación), cuenta éxitos/fallos y refresca la página.</summary>
    private async Task EjecutarEnLoteAsync(
        IReadOnlyList<ReciboItem> objetivos,
        Func<ReciboItem, Task<ServiceResult<bool>>> accion,
        string mensajeUno, string mensajeVarios)
    {
        var ok = 0;
        string? ultimoError = null;
        await EjecutarConProgresoAsync("Aplicando cambios", async (progreso, ct) =>
        {
            var total = objetivos.Count;
            var i = 0;
            foreach (var item in objetivos)
            {
                ct.ThrowIfCancellationRequested();
                progreso.Report(new ProgresoMasivo(++i, total, item.Empresa));
                var res = await accion(item);
                if (res.Success) ok++;
                else ultimoError = res.ErrorMessage;
            }
        });
        await CargarPaginaAsync();
        var fallos = objetivos.Count - ok;
        if (ok > 0)
            MostrarExito(ok == 1 && fallos == 0 ? mensajeUno : $"{ok} {mensajeVarios}"
                + (fallos > 0 ? $" {fallos} con error: {ultimoError}" : string.Empty));
        else
            MostrarError(ultimoError ?? "No se pudo completar la acción.");
    }
}
