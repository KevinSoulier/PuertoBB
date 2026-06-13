using System.Windows.Controls;
using CamaraPortuaria.UI.ViewModels;

namespace CamaraPortuaria.UI.Views;

public partial class RecibosPage : Page
{
    public RecibosPage(RecibosViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
