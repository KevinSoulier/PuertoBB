using System.Windows.Controls;
using System.Windows.Input;
using CentroMaritimo.UI.ViewModels;

namespace CentroMaritimo.UI.Views;

public partial class VouchersPage : Page
{
    public VouchersPage(VouchersViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void ComboBox_OpenOnDownKey(object sender, KeyEventArgs e)
    {
        if (sender is not ComboBox cb || cb.IsDropDownOpen) return;

        // Down → abrir el dropdown y dejar que la primera opción reciba foco.
        if (e.Key == Key.Down)
        {
            cb.IsDropDownOpen = true;
            e.Handled = true;
            return;
        }

        // Caracter imprimible (letra/dígito/símbolo) → abrir el dropdown sin consumir la tecla,
        // así el caracter cae en el TextBox interno y dispara el filtrado del VM.
        if (EsTeclaImprimible(e.Key) && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == 0)
        {
            cb.IsDropDownOpen = true;
            // NO marcamos e.Handled — el texto debe seguir su curso normal hacia el TextBox.
        }
    }

    private static bool EsTeclaImprimible(Key k) =>
        (k >= Key.A && k <= Key.Z) ||
        (k >= Key.D0 && k <= Key.D9) ||
        (k >= Key.NumPad0 && k <= Key.NumPad9) ||
        k is Key.OemMinus or Key.OemPlus or Key.OemComma or Key.OemPeriod
          or Key.OemQuestion or Key.OemSemicolon or Key.OemQuotes or Key.Space;

    private void VouchersGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Sólo cuando el doble-click cae sobre una fila (no encabezados, no área vacía).
        if (e.OriginalSource is System.Windows.DependencyObject src &&
            ItemsControl.ContainerFromElement((DataGrid)sender, src) is DataGridRow &&
            DataContext is VouchersViewModel vm &&
            vm.EditarCommand.CanExecute(null))
        {
            vm.EditarCommand.Execute(null);
        }
    }
}
