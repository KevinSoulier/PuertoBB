using System.Windows.Controls;
using CentroMaritimo.UI.ViewModels;

namespace CentroMaritimo.UI.Views;

public partial class ControlPagosPage : Page
{
    public ControlPagosPage(ControlPagosViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
