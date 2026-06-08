using System.Windows.Controls;
using System.Windows.Input;
using CamaraPortuaria.UI.ViewModels;

namespace CamaraPortuaria.UI.Views;

public partial class RecibosPage : Page
{
    public RecibosPage(RecibosViewModel vm)
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

        // Caracter imprimible → abrir el dropdown sin consumir la tecla, así el texto cae en el
        // TextBox interno y dispara el filtrado del VM.
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
}
