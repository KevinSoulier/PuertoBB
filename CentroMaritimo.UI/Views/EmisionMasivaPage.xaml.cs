using System.Windows.Controls;
using CentroMaritimo.UI.ViewModels;

namespace CentroMaritimo.UI.Views;

public partial class EmisionMasivaPage : Page
{
    public EmisionMasivaPage(EmisionMasivaViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
