using System.Windows.Controls;
using CamaraPortuaria.UI.ViewModels;

namespace CamaraPortuaria.UI.Views;

public partial class ControlPagosPage : Page
{
    public ControlPagosPage(ControlPagosViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
