using System.Windows.Controls;
using CentroMaritimo.UI.ViewModels;

namespace CentroMaritimo.UI.Views;

public partial class ConceptosReciboPage : Page
{
    public ConceptosReciboPage(ConceptosReciboViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
