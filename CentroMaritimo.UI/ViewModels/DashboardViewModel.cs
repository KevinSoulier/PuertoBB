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
    public ICommand IrACierrePeriodoCommand { get; }
    public ICommand IrARecibosCommand { get; }
    public ICommand IrAEmisionMasivaCommand { get; }
    public ICommand IrAControlPagosCommand { get; }

    public DashboardViewModel(ICentroMaritimoReciboService recibos, IVoucherService vouchers, INavigationService nav)
    {
        _recibos = recibos;
        _vouchers = vouchers;
        _nav = nav;

        IrAVouchersCommand      = new RelayCommand(_ => _nav.Navigate(typeof(VouchersPage)));
        IrACierrePeriodoCommand = new RelayCommand(_ => _nav.Navigate(typeof(CierrePeriodoPage)));
        IrARecibosCommand       = new RelayCommand(_ => _nav.Navigate(typeof(RecibosPage)));
        IrAEmisionMasivaCommand = new RelayCommand(_ => _nav.Navigate(typeof(EmisionMasivaPage)));
        IrAControlPagosCommand  = new RelayCommand(_ => _nav.Navigate(typeof(ControlPagosPage)));

        CargarSeguro(CargarResumenAsync);
    }

    private Task CargarResumenAsync()
        => EjecutarOcupadoAsync("Cargando", async () =>
        {
            var hoy = DateTime.Today;
            var vouchers = await _vouchers.GetPendientesAsync(hoy.Year, hoy.Month);
            ResumenVouchers = vouchers.Success ? (vouchers.Data?.Count ?? 0).ToString() : "–";

            var pendientes = await _recibos.GetPendientesAsync(new FiltroPendientes { SoloVencidos = false });
            if (pendientes.Success && pendientes.Data is not null)
            {
                var vencidos = pendientes.Data.Count(r =>
                    PuertoBB.Core.Common.EstadoReciboHelper.EstaVencido(r, hoy));
                ResumenRecibosVencidos = vencidos.ToString();
                ResumenRecibosPendientes = pendientes.Data.Count.ToString();
            }
        });
}
