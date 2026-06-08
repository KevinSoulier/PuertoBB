using System.Windows.Controls;
using CamaraPortuaria.UI.ViewModels;

namespace CamaraPortuaria.UI.Views;

public partial class ConceptosReciboPage : Page
{
    public ConceptosReciboPage(ConceptosReciboViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
