using System.Windows.Controls;
using CamaraPortuaria.UI.ViewModels;

namespace CamaraPortuaria.UI.Views;

public partial class DashboardPage : Page
{
    public DashboardPage(DashboardViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
