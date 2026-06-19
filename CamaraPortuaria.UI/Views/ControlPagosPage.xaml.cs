using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using CamaraPortuaria.UI.ViewModels;
using CamaraPortuaria.UI.ViewModels.Items;

namespace CamaraPortuaria.UI.Views;

public partial class ControlPagosPage : Page
{
    public ControlPagosPage(ControlPagosViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    // WPF DataGrid.SelectedItems no es bindeable: lo empujamos al VM en cada cambio de selección.
    private void Recibos_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not ControlPagosViewModel vm) return;
        vm.Seleccionados = RecibosGrid.SelectedItems.Cast<ReciboItem>().ToList();
        CommandManager.InvalidateRequerySuggested();
    }
}
