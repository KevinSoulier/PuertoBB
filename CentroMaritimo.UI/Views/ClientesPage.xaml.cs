using System.Windows.Controls;
using CentroMaritimo.UI.ViewModels;

namespace CentroMaritimo.UI.Views;

public partial class ClientesPage : Page
{
    public ClientesPage(ClientesViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
