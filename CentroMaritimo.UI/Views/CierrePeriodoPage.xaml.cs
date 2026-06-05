using System.Windows.Controls;
using CentroMaritimo.UI.ViewModels;

namespace CentroMaritimo.UI.Views;

public partial class CierrePeriodoPage : Page
{
    public CierrePeriodoPage(CierrePeriodoViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
