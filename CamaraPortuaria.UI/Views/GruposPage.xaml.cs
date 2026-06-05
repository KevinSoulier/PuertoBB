using System.Windows.Controls;
using CamaraPortuaria.UI.ViewModels;

namespace CamaraPortuaria.UI.Views;

public partial class GruposPage : Page
{
    public GruposPage(GruposViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
