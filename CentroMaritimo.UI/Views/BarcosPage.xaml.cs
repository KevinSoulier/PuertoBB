using System.Windows.Controls;
using CentroMaritimo.UI.ViewModels;

namespace CentroMaritimo.UI.Views;

public partial class BarcosPage : Page
{
    public BarcosPage(BarcosViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
