using System.Collections.ObjectModel;
using CentroMaritimo.UI.ViewModels.Base;
using CentroMaritimo.UI.Views;
using Wpf.Ui.Controls;

namespace CentroMaritimo.UI.ViewModels;

public class MainWindowViewModel : BaseViewModel
{
    public ObservableCollection<object> NavigationItems { get; } =
    [
        new NavigationViewItem("Inicio",            SymbolRegular.Home24,              typeof(DashboardPage)),
        new NavigationViewItem("Vouchers",          SymbolRegular.TicketDiagonal24,    typeof(VouchersPage)),
        new NavigationViewItem("Cierre de período", SymbolRegular.CalendarCheckmark24, typeof(CierrePeriodoPage)),
        new NavigationViewItem("Recibos",           SymbolRegular.ReceiptMoney24,      typeof(RecibosPage)),
        new NavigationViewItem("Emisión masiva",    SymbolRegular.DocumentMultiple24,  typeof(EmisionMasivaPage)),
        new NavigationViewItem("Agencias",          SymbolRegular.BuildingShop24,      typeof(AgenciasPage)),
        new NavigationViewItem("Barcos",            SymbolRegular.VehicleShip24,       typeof(BarcosPage)),
        new NavigationViewItem("Grupos",            SymbolRegular.PeopleTeam24,        typeof(GruposPage)),
    ];

    public ObservableCollection<object> NavigationFooter { get; } =
    [
        new NavigationViewItem("Configuración", SymbolRegular.Settings24, typeof(ConfiguracionPage)),
    ];
}
