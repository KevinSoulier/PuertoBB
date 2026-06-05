using System.Windows.Controls;
using CentroMaritimo.UI.ViewModels;

namespace CentroMaritimo.UI.Views;

public partial class AgenciasPage : Page
{
    public AgenciasPage(AgenciasViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
