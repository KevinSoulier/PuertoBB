using System.Collections.ObjectModel;
using CamaraPortuaria.UI.ViewModels.Base;
using CamaraPortuaria.UI.Views;
using Wpf.Ui.Controls;

namespace CamaraPortuaria.UI.ViewModels;

public class MainWindowViewModel : BaseViewModel
{
    public ObservableCollection<object> NavigationItems { get; } =
    [
        new NavigationViewItem("Inicio",         SymbolRegular.Home24,             typeof(DashboardPage)),
        new NavigationViewItem("Recibos",        SymbolRegular.ReceiptMoney24,     typeof(RecibosPage)),
        new NavigationViewItem("Emisión masiva", SymbolRegular.DocumentMultiple24, typeof(EmisionMasivaPage)),
        new NavigationViewItem("Empresas",       SymbolRegular.BuildingMultiple24, typeof(EmpresasPage)),
        new NavigationViewItem("Grupos",         SymbolRegular.PeopleTeam24,       typeof(GruposPage)),
    ];

    public ObservableCollection<object> NavigationFooter { get; } =
    [
        new NavigationViewItem("Configuración", SymbolRegular.Settings24, typeof(ConfiguracionPage)),
    ];
}
