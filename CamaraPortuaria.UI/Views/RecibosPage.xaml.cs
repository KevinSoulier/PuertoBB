using System.Windows;
using System.Windows.Controls;
using CamaraPortuaria.UI.ViewModels;
using CamaraPortuaria.UI.ViewModels.Items;

namespace CamaraPortuaria.UI.Views;

public partial class RecibosPage : Page
{
    private RecibosViewModel Vm => (RecibosViewModel)DataContext;

    public RecibosPage(RecibosViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void Reintentar_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ReciboItem item)
        { Vm.Seleccionado = item; Vm.ReintentarCommand.Execute(null); }
    }

    private void Anular_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ReciboItem item)
        { Vm.Seleccionado = item; Vm.AnularCommand.Execute(null); }
    }

    private void Reenviar_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ReciboItem item)
        { Vm.Seleccionado = item; Vm.ReenviarCommand.Execute(null); }
    }

    private void MarcarPagado_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ReciboItem item)
        { Vm.Seleccionado = item; Vm.MarcarPagadoCommand.Execute(null); }
    }

    private void Previsualizar_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ReciboItem item)
        { Vm.Seleccionado = item; Vm.PrevisualizarCommand.Execute(null); }
    }
}
