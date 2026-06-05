using System.Collections.ObjectModel;
using System.Windows.Input;
using CentroMaritimo.UI.ViewModels.Base;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Resultados;
using PuertoBB.Services.Common;

namespace CentroMaritimo.UI.ViewModels;

public class CierrePeriodoViewModel : PageViewModel
{
    private readonly IVoucherService _voucherService;
    private readonly ICentroMaritimoReciboService _reciboService;
    private readonly IDialogService _dialog;

    public ObservableCollection<ResultadoCierrePorAgencia> Resultados { get; } = [];
    public IReadOnlyList<int> Anios { get; } = Enumerable.Range(DateTime.Today.Year - 5, 7).Reverse().ToList();
    public IReadOnlyList<int> Meses { get; } = Enumerable.Range(1, 12).ToList();

    private int _anio = DateTime.Today.Year;
    public int Anio { get => _anio; set => SetField(ref _anio, value); }

    private int _mes = DateTime.Today.Month;
    public int Mes { get => _mes; set => SetField(ref _mes, value); }

    private string _resumen = string.Empty;
    public string Resumen { get => _resumen; set => SetField(ref _resumen, value); }

    public ICommand PreviewCommand { get; }
    public ICommand CerrarCommand { get; }

    public CierrePeriodoViewModel(IVoucherService voucherService, ICentroMaritimoReciboService reciboService, IDialogService dialog)
    {
        _voucherService = voucherService;
        _reciboService = reciboService;
        _dialog = dialog;
        PreviewCommand = new AsyncRelayCommand(PreviewAsync);
        CerrarCommand = new AsyncRelayCommand(CerrarAsync);
    }

    private async Task PreviewAsync()
    {
        LimpiarStatus();
        var res = await _voucherService.GetPendientesAsync(Anio, Mes);
        if (!res.Success || res.Data is null) { MostrarError(res.ErrorMessage ?? "No se pudo consultar."); return; }

        var porAgencia = res.Data.GroupBy(v => v.AgenciaId).ToList();
        var total = res.Data.Sum(v => v.Importe);
        Resumen = $"{porAgencia.Count} agencia(s) con vouchers pendientes · {res.Data.Count} voucher(s) · Total {Formato.Moneda(total)}";
    }

    private async Task CerrarAsync()
    {
        LimpiarStatus();
        var pre = await _voucherService.GetPendientesAsync(Anio, Mes);
        var cantAgencias = pre.Data?.GroupBy(v => v.AgenciaId).Count() ?? 0;
        if (cantAgencias == 0) { MostrarError("No hay vouchers pendientes en el período."); return; }

        if (!await _dialog.ShowConfirmAsync("Cerrar período",
                $"Se generará un recibo consolidado para {cantAgencias} agencia(s) del período {Formato.Periodo(Anio, Mes)}. ¿Continuar?")) return;

        IsBusy = true;
        Resultados.Clear();
        try
        {
            var res = await _reciboService.CerrarPeriodoAsync(Anio, Mes);
            if (!res.Success) { MostrarError(res.ErrorMessage ?? "No se pudo cerrar el período."); return; }
            foreach (var r in res.Data!) Resultados.Add(r);
            var ok = Resultados.Count(r => r.Exito);
            MostrarExito($"Cierre finalizado: {ok} recibo(s) generado(s), {Resultados.Count - ok} con observaciones.");
        }
        finally { IsBusy = false; }
    }
}
