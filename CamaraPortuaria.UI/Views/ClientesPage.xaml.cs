using System.Windows.Controls;
using CamaraPortuaria.UI.ViewModels;

namespace CamaraPortuaria.UI.Views;

public partial class ClientesPage : Page
{
    public ClientesPage(ClientesViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
