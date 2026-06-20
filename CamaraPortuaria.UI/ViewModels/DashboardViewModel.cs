using System.Windows.Input;
using CamaraPortuaria.UI.ViewModels.Base;
using CamaraPortuaria.UI.Views;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Resultados;
using Wpf.Ui;

namespace CamaraPortuaria.UI.ViewModels;

public class DashboardViewModel : PageViewModel
{
    private readonly ICamaraPortuariaReciboService _recibos;
    private readonly INavigationService _nav;

    private string _resumenRecibosVencidos = "–";
    public string ResumenRecibosVencidos { get => _resumenRecibosVencidos; set => SetField(ref _resumenRecibosVencidos, value); }

    private string _resumenRecibosPendientes = "–";
    public string ResumenRecibosPendientes { get => _resumenRecibosPendientes; set => SetField(ref _resumenRecibosPendientes, value); }

    public ICommand IrARecibosCommand { get; }
    public ICommand IrAEmisionMasivaCommand { get; }
    public ICommand IrAControlPagosCommand { get; }

    public DashboardViewModel(ICamaraPortuariaReciboService recibos, INavigationService nav)
    {
        _recibos = recibos;
        _nav = nav;

        IrARecibosCommand       = new RelayCommand(_ => _nav.Navigate(typeof(RecibosPage)));
        IrAEmisionMasivaCommand = new RelayCommand(_ => _nav.Navigate(typeof(EmisionMasivaPage)));
        IrAControlPagosCommand  = new RelayCommand(_ => _nav.Navigate(typeof(ControlPagosPage)));

        CargarSeguro(CargarResumenAsync);
    }

    private Task CargarResumenAsync()
        => EjecutarOcupadoAsync("Cargando", async () =>
        {
            var hoy = DateTime.Today;
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
