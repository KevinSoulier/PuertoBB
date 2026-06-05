using System.Windows.Controls;
using CamaraPortuaria.UI.ViewModels;

namespace CamaraPortuaria.UI.Views;

public partial class EmpresasPage : Page
{
    public EmpresasPage(EmpresasViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
