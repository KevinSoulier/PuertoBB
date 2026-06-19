using System.Windows;
using System.Windows.Controls;
using CamaraPortuaria.UI.ViewModels;
using CamaraPortuaria.UI.ViewModels.Items;

namespace CamaraPortuaria.UI.Views;

public partial class EmisionMasivaPage : Page
{
    private EmisionMasivaViewModel Vm => (EmisionMasivaViewModel)DataContext;

    public EmisionMasivaPage(EmisionMasivaViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void Emitir_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is EmisionMasivaItem item)
        { Vm.Seleccionado = item; Vm.EmitirCommand.Execute(null); }
    }

    private void Enviar_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is EmisionMasivaItem item)
        { Vm.Seleccionado = item; Vm.EnviarCommand.Execute(null); }
    }

    private void Previsualizar_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is EmisionMasivaItem item)
        { Vm.Seleccionado = item; Vm.PrevisualizarCommand.Execute(null); }
    }

    private void Eliminar_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is EmisionMasivaItem item)
        { Vm.Seleccionado = item; Vm.EliminarCommand.Execute(null); }
    }
}
