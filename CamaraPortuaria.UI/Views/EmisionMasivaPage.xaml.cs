using System.Windows.Controls;
using CamaraPortuaria.UI.ViewModels;

namespace CamaraPortuaria.UI.Views;

public partial class EmisionMasivaPage : Page
{
    public EmisionMasivaPage(EmisionMasivaViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
