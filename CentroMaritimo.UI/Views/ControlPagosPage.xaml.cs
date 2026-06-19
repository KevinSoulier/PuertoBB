using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using CentroMaritimo.UI.ViewModels;
using CentroMaritimo.UI.ViewModels.Items;

namespace CentroMaritimo.UI.Views;

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
