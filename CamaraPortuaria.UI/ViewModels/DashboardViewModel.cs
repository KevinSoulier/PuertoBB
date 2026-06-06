using System.Windows.Input;
using CamaraPortuaria.UI.ViewModels.Base;
using CamaraPortuaria.UI.Views;
using PuertoBB.Core.Common;
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

    public ICommand IrAControlPagosCommand { get; }
    public ICommand IrAEmisionMasivaCommand { get; }
    public ICommand IrAEmpresasCommand { get; }
    public ICommand IrAGruposCommand { get; }

    public DashboardViewModel(ICamaraPortuariaReciboService recibos, INavigationService nav)
    {
        _recibos = recibos;
        _nav = nav;

        IrAControlPagosCommand  = new RelayCommand(_ => _nav.Navigate(typeof(ControlPagosPage)));
        IrAEmisionMasivaCommand = new RelayCommand(_ => _nav.Navigate(typeof(EmisionMasivaPage)));
        IrAEmpresasCommand      = new RelayCommand(_ => _nav.Navigate(typeof(EmpresasPage)));
        IrAGruposCommand        = new RelayCommand(_ => _nav.Navigate(typeof(GruposPage)));

        _ = CargarResumenAsync();
    }

    private async Task CargarResumenAsync()
    {
        IsBusy = true;
        try
        {
            var hoy = DateTime.Today;
            var pendientes = await _recibos.GetPendientesAsync(new FiltroPendientes { SoloVencidos = false });
            if (pendientes.Success && pendientes.Data is not null)
            {
                var vencidos = pendientes.Data.Count(r =>
                    EstadoReciboHelper.EstaVencido(r.Estado, r.FechaVencimientoPago, hoy));
                ResumenRecibosVencidos = vencidos.ToString();
                ResumenRecibosPendientes = pendientes.Data.Count.ToString();
            }
        }
        finally { IsBusy = false; }
    }
}
