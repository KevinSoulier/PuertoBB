using System.Windows.Controls;
using CentroMaritimo.UI.ViewModels;

namespace CentroMaritimo.UI.Views;

public partial class VouchersPage : Page
{
    public VouchersPage(VouchersViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
