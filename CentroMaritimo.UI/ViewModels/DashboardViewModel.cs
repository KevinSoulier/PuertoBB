using System.Collections.ObjectModel;
using System.Windows.Input;
using CentroMaritimo.UI.ViewModels.Base;
using CentroMaritimo.UI.ViewModels.Items;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Resultados;

namespace CentroMaritimo.UI.ViewModels;

public class DashboardViewModel : PageViewModel
{
    private readonly ICentroMaritimoReciboService _recibos;
    private readonly IDialogService _dialog;

    public ObservableCollection<ReciboItem> Recibos { get; } = [];

    private bool _soloVencidos;
    public bool SoloVencidos { get => _soloVencidos; set => SetField(ref _soloVencidos, value); }

    private ReciboItem? _seleccionado;
    public ReciboItem? Seleccionado { get => _seleccionado; set => SetField(ref _seleccionado, value); }

    private string _resumen = string.Empty;
    public string Resumen { get => _resumen; set => SetField(ref _resumen, value); }

    public ICommand BuscarCommand { get; }
    public ICommand MarcarPagadoCommand { get; }
    public ICommand ReenviarCommand { get; }

    public DashboardViewModel(ICentroMaritimoReciboService recibos, IDialogService dialog)
    {
        _recibos = recibos;
        _dialog = dialog;
        BuscarCommand = new AsyncRelayCommand(BuscarAsync);
        MarcarPagadoCommand = new AsyncRelayCommand(MarcarPagadoAsync, () => Seleccionado?.EsPagable == true);
        ReenviarCommand = new AsyncRelayCommand(ReenviarAsync, () => Seleccionado?.EsReenviable == true);
        _ = BuscarAsync();
    }

    private async Task BuscarAsync()
    {
        IsBusy = true;
        LimpiarStatus();
        try
        {
            var res = await _recibos.GetPendientesAsync(new FiltroPendientes { SoloVencidos = SoloVencidos });
            Recibos.Clear();
            if (res.Success && res.Data is not null)
                foreach (var r in res.Data) Recibos.Add(new ReciboItem(r));
            var vencidos = Recibos.Count(r => r.Estado == "Vencido");
            Resumen = $"{Recibos.Count} recibo(s) · {vencidos} vencido(s)";
        }
        finally { IsBusy = false; }
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
}
