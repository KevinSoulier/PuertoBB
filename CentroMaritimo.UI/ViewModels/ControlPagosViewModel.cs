using System.Collections.ObjectModel;
using System.Windows.Input;
using CentroMaritimo.UI.ViewModels.Base;
using CentroMaritimo.UI.ViewModels.Items;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Resultados;

namespace CentroMaritimo.UI.ViewModels;

public class ControlPagosViewModel : PageViewModel
{
    private readonly ICentroMaritimoReciboService _recibos;
    private readonly IAgenciaRepository _agencias;
    private readonly IDialogService _dialog;

    private List<ReciboItem> _todosRecibos = [];
    public ObservableCollection<ReciboItem> Recibos { get; private set; } = [];

    private bool _soloVencidos;
    public bool SoloVencidos { get => _soloVencidos; set { if (SetField(ref _soloVencidos, value)) _ = BuscarAsync(); } }

    private bool _incluirMorosos;
    public bool IncluirMorosos
    {
        get => _incluirMorosos;
        set { if (SetField(ref _incluirMorosos, value)) _ = BuscarAsync(); }
    }

    private string _textoBusqueda = string.Empty;
    public string TextoBusqueda
    {
        get => _textoBusqueda;
        set { if (SetField(ref _textoBusqueda, value)) AplicarFiltro(); }
    }

    public IReadOnlyList<string> EstadosFiltro { get; } =
        ["Todos", "Emitido", "Enviado", "Vencido", "Pendiente", "Moroso"];

    private string _filtroEstado = "Todos";
    public string FiltroEstado
    {
        get => _filtroEstado;
        set { if (SetField(ref _filtroEstado, value)) AplicarFiltro(); }
    }

    private ReciboItem? _seleccionado;
    public ReciboItem? Seleccionado { get => _seleccionado; set => SetField(ref _seleccionado, value); }

    private string _resumen = string.Empty;
    public string Resumen { get => _resumen; set => SetField(ref _resumen, value); }

    public ICommand BuscarCommand { get; }
    public ICommand MarcarPagadoCommand { get; }
    public ICommand ReenviarCommand { get; }
    public ICommand MarcarMorosoCommand { get; }

    public ControlPagosViewModel(ICentroMaritimoReciboService recibos, IAgenciaRepository agencias, IDialogService dialog)
    {
        _recibos = recibos;
        _agencias = agencias;
        _dialog = dialog;
        BuscarCommand = new AsyncRelayCommand(BuscarAsync);
        MarcarPagadoCommand = new AsyncRelayCommand(MarcarPagadoAsync, () => Seleccionado?.EsPagable == true);
        ReenviarCommand = new AsyncRelayCommand(ReenviarAsync, () => Seleccionado?.EsReenviable == true);
        MarcarMorosoCommand = new AsyncRelayCommand(MarcarMorosoAsync, () => Seleccionado is not null);
        _ = BuscarAsync();
    }

    private async Task BuscarAsync()
    {
        IsBusy = true;
        LimpiarStatus();
        try
        {
            var filtro = new FiltroPendientes { SoloVencidos = SoloVencidos, ExcluirMorosos = !IncluirMorosos };
            var res = await _recibos.GetPendientesAsync(filtro);
            _todosRecibos = res.Success && res.Data is not null
                ? res.Data.Select(r => new ReciboItem(r)).ToList()
                : [];
            AplicarFiltro();
        }
        finally { IsBusy = false; }
    }

    private void AplicarFiltro()
    {
        var lista = (IEnumerable<ReciboItem>)_todosRecibos;
        var texto = _textoBusqueda.Trim();
        if (!string.IsNullOrEmpty(texto))
            lista = lista.Where(r =>
                r.Agencia.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                r.Comprobante.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                r.Periodo.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                r.Estado.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                r.Importe.Contains(texto, StringComparison.OrdinalIgnoreCase));
        if (_filtroEstado != "Todos")
            lista = lista.Where(r => r.Estado == _filtroEstado);
        Recibos = new ObservableCollection<ReciboItem>(lista);
        OnPropertyChanged(nameof(Recibos));
        var vencidos = Recibos.Count(r => r.Estado == "Vencido");
        Resumen = $"{Recibos.Count} recibo(s) · {vencidos} vencido(s)";
    }

    private async Task MarcarPagadoAsync()
    {
        if (Seleccionado is null) return;
        if (!await _dialog.ShowConfirmAsync("Marcar como pagado",
                $"¿Marcar el recibo {Seleccionado.Comprobante} de {Seleccionado.Agencia} como pagado?")) return;
        var res = await _recibos.MarcarPagadoAsync(Seleccionado.Id);
        if (res.Success) { MostrarExito("Recibo marcado como pagado."); await BuscarAsync(); }
        else MostrarError(res.ErrorMessage ?? "No se pudo marcar.");
    }

    private async Task ReenviarAsync()
    {
        if (Seleccionado is null) return;
        var res = await _recibos.ReenviarMailAsync(Seleccionado.Id);
        if (res.Success) { MostrarExito("Recibo reenviado."); await BuscarAsync(); }
        else MostrarError(res.ErrorMessage ?? "No se pudo reenviar.");
    }

    private async Task MarcarMorosoAsync()
    {
        if (Seleccionado is null) return;
        var nuevoEstado = !Seleccionado.EsMoroso;
        var accion = nuevoEstado ? "marcar como morosa" : "quitar estado moroso de";
        if (!await _dialog.ShowConfirmAsync("Estado moroso",
                $"¿Desea {accion} la agencia {Seleccionado.Agencia}?")) return;
        await _agencias.SetMorosoAsync(Seleccionado.AgenciaId, nuevoEstado);
        MostrarExito(nuevoEstado ? "Agencia marcada como morosa." : "Estado moroso removido.");
        await BuscarAsync();
    }
}
