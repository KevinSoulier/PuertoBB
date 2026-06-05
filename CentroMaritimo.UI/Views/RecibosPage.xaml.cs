using System.Windows.Controls;
using CentroMaritimo.UI.ViewModels;

namespace CentroMaritimo.UI.Views;

public partial class RecibosPage : Page
{
    public RecibosPage(RecibosViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
