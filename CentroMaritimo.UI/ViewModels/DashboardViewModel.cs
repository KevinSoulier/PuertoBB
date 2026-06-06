using System.Windows.Input;
using CentroMaritimo.UI.ViewModels.Base;
using CentroMaritimo.UI.Views;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Resultados;
using Wpf.Ui;

namespace CentroMaritimo.UI.ViewModels;

public class DashboardViewModel : PageViewModel
{
    private readonly ICentroMaritimoReciboService _recibos;
    private readonly IVoucherService _vouchers;
    private readonly INavigationService _nav;

    private string _resumenVouchers = "–";
    public string ResumenVouchers { get => _resumenVouchers; set => SetField(ref _resumenVouchers, value); }

    private string _resumenRecibosVencidos = "–";
    public string ResumenRecibosVencidos { get => _resumenRecibosVencidos; set => SetField(ref _resumenRecibosVencidos, value); }

    private string _resumenRecibosPendientes = "–";
    public string ResumenRecibosPendientes { get => _resumenRecibosPendientes; set => SetField(ref _resumenRecibosPendientes, value); }

    public ICommand IrAVouchersCommand { get; }
    public ICommand IrAControlPagosCommand { get; }
    public ICommand IrAEmisionMasivaCommand { get; }
    public ICommand IrAAgenciasCommand { get; }
    public ICommand IrABarcosCommand { get; }
    public ICommand IrAGruposCommand { get; }

    public DashboardViewModel(ICentroMaritimoReciboService recibos, IVoucherService vouchers, INavigationService nav)
    {
        _recibos = recibos;
        _vouchers = vouchers;
        _nav = nav;

        IrAVouchersCommand     = new RelayCommand(_ => _nav.Navigate(typeof(VouchersPage)));
        IrAControlPagosCommand = new RelayCommand(_ => _nav.Navigate(typeof(ControlPagosPage)));
        IrAEmisionMasivaCommand = new RelayCommand(_ => _nav.Navigate(typeof(EmisionMasivaPage)));
        IrAAgenciasCommand     = new RelayCommand(_ => _nav.Navigate(typeof(AgenciasPage)));
        IrABarcosCommand       = new RelayCommand(_ => _nav.Navigate(typeof(BarcosPage)));
        IrAGruposCommand       = new RelayCommand(_ => _nav.Navigate(typeof(GruposPage)));

        _ = CargarResumenAsync();
    }

    private async Task CargarResumenAsync()
    {
        IsBusy = true;
        try
        {
            var hoy = DateTime.Today;
            var vouchers = await _vouchers.GetPendientesAsync(hoy.Year, hoy.Month);
            ResumenVouchers = vouchers.Success ? (vouchers.Data?.Count ?? 0).ToString() : "–";

            var pendientes = await _recibos.GetPendientesAsync(new FiltroPendientes { SoloVencidos = false });
            if (pendientes.Success && pendientes.Data is not null)
            {
                var vencidos = pendientes.Data.Count(r =>
                    PuertoBB.Core.Common.EstadoReciboHelper.EstaVencido(r.Estado, r.FechaVencimientoPago, hoy));
                ResumenRecibosVencidos = vencidos.ToString();
                ResumenRecibosPendientes = pendientes.Data.Count.ToString();
            }
        }
        finally { IsBusy = false; }
    }
}
