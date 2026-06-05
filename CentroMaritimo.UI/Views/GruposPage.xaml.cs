using System.Windows.Controls;
using CentroMaritimo.UI.ViewModels;

namespace CentroMaritimo.UI.Views;

public partial class GruposPage : Page
{
    public GruposPage(GruposViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
