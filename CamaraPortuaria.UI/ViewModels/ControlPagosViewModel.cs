using System.Collections.ObjectModel;
using System.Windows.Input;
using CamaraPortuaria.UI.ViewModels.Base;
using CamaraPortuaria.UI.ViewModels.Items;
using PuertoBB.Core.Common;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Resultados;

namespace CamaraPortuaria.UI.ViewModels;

public class ControlPagosViewModel : PageViewModel
{
    private readonly ICamaraPortuariaReciboService _recibos;
    private readonly IDialogService _dialog;

    private List<ReciboItem> _todosRecibos = [];
    public ObservableCollection<ReciboItem> Recibos { get; private set; } = [];

    private bool _soloVencidos;
    public bool SoloVencidos { get => _soloVencidos; set { if (SetField(ref _soloVencidos, value)) AplicarFiltro(); } }

    private bool _incluirIncobrables;
    public bool IncluirIncobrables
    {
        get => _incluirIncobrables;
        set { if (SetField(ref _incluirIncobrables, value)) AplicarFiltro(); }
    }

    private string _textoBusqueda = string.Empty;
    public string TextoBusqueda
    {
        get => _textoBusqueda;
        set { if (SetField(ref _textoBusqueda, value)) AplicarFiltro(); }
    }

    public IReadOnlyList<string> EstadosFiltro { get; } =
        ["Pendientes de pago", "Emitido", "Vencido", "Pagado", "Incobrable", "Anulado", "Todos"];

    private string _filtroEstado = "Pendientes de pago";
    public string FiltroEstado
    {
        get => _filtroEstado;
        set { if (SetField(ref _filtroEstado, value)) AplicarFiltro(); }
    }

    private ReciboItem? _seleccionado;
    public ReciboItem? Seleccionado { get => _seleccionado; set => SetField(ref _seleccionado, value); }

    /// <summary>Filas multiseleccionadas en la grilla (la setea el code-behind en SelectionChanged).</summary>
    public IReadOnlyList<ReciboItem> Seleccionados { get; set; } = [];

    private string _resumen = string.Empty;
    public string Resumen { get => _resumen; set => SetField(ref _resumen, value); }

    public ICommand BuscarCommand { get; }
    public ICommand MarcarPagadoCommand { get; }
    public ICommand ReenviarCommand { get; }
    public ICommand MarcarIncobrableCommand { get; }
    public ICommand QuitarIncobrableCommand { get; }

    public ControlPagosViewModel(ICamaraPortuariaReciboService recibos, IDialogService dialog)
    {
        _recibos = recibos;
        _dialog = dialog;
        BuscarCommand = new AsyncRelayCommand(BuscarAsync);
        MarcarPagadoCommand = new AsyncRelayCommand(MarcarPagadoAsync, () => Seleccionados.Any(x => x.EsPagable));
        ReenviarCommand = new AsyncRelayCommand(ReenviarAsync, () => Seleccionado?.EsReenviable == true);
        MarcarIncobrableCommand = new AsyncRelayCommand(MarcarIncobrableAsync, () => Seleccionados.Any(x => x.EsMarcableIncobrable));
        QuitarIncobrableCommand = new AsyncRelayCommand(QuitarIncobrableAsync, () => Seleccionados.Any(x => x.EsQuitableIncobrable));
        CargarSeguro(BuscarAsync);
    }

    private Task BuscarAsync()
    {
        LimpiarStatus();
        return EjecutarOcupadoAsync("Actualizando", async () =>
        {
            // Cargamos todo (incluso incobrables/pagados/anulados) y filtramos client-side en AplicarFiltro.
            var res = await _recibos.GetPendientesAsync(new FiltroPendientes { ExcluirIncobrables = false });
            _todosRecibos = res.Success && res.Data is not null
                ? res.Data.Select(r => new ReciboItem(r)).ToList()
                : [];
            AplicarFiltro();
        });
    }

    private void AplicarFiltro()
    {
        var lista = (IEnumerable<ReciboItem>)_todosRecibos;
        var texto = _textoBusqueda.Trim();
        if (!string.IsNullOrEmpty(texto))
            lista = lista.Where(r => Coincide(r, texto));

        // "Pendientes de pago" (default): vista de cobranza modulada por los checkboxes.
        // Un estado puntual o "Todos" ignoran los checkboxes.
        lista = _filtroEstado switch
        {
            "Pendientes de pago" => lista.Where(r =>
                (SoloVencidos ? r.Estado == "Vencido" : r.Estado is "Emitido" or "Vencido")
                || (IncluirIncobrables && r.Estado == "Incobrable")),
            "Todos" => lista,
            _ => lista.Where(r => r.Estado == _filtroEstado),
        };

        Recibos = new ObservableCollection<ReciboItem>(lista);
        OnPropertyChanged(nameof(Recibos));
        var vencidos = Recibos.Count(r => r.Estado == "Vencido");
        Resumen = $"{Recibos.Count} recibo(s) · {vencidos} vencido(s)";
    }

    /// <summary>True si el texto matchea cualquier columna visible de la grilla.</summary>
    private static bool Coincide(ReciboItem r, string texto)
    {
        string[] campos =
        [
            r.Empresa, r.Comprobante, r.Periodo, r.Importe, r.FechaEmision,
            r.FechaVencimiento, r.DiasAtraso.ToString(), r.Estado, r.EstadoEnvio,
        ];
        return campos.Any(c => c.Contains(texto, StringComparison.OrdinalIgnoreCase));
    }

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
            if (res.Success) { MostrarExito("Recibo reenviado."); await BuscarAsync(); }
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

    /// <summary>Aplica una acción a cada recibo (con progreso y cancelación), cuenta éxitos/fallos y refresca la lista.</summary>
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
        await BuscarAsync();
        var fallos = objetivos.Count - ok;
        if (ok > 0)
            MostrarExito(ok == 1 && fallos == 0 ? mensajeUno : $"{ok} {mensajeVarios}"
                + (fallos > 0 ? $" {fallos} con error: {ultimoError}" : string.Empty));
        else
            MostrarError(ultimoError ?? "No se pudo completar la acción.");
    }
}
