using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CentroMaritimo.UI.Behaviors;

/// <summary>
/// Behavior adjunto para que un control scrolleable interno (p. ej. un <see cref="System.Windows.Controls.DataGrid"/>
/// anidado en el RowDetails de otro DataGrid) no "atrape" la rueda del mouse: reenvía el evento al
/// contenedor padre para que el listado externo siga desplazándose de forma continua.
/// Uso en XAML: <c>behaviors:MouseWheel.BubbleToParent="True"</c>.
/// </summary>
public static class MouseWheel
{
    public static readonly DependencyProperty BubbleToParentProperty =
        DependencyProperty.RegisterAttached(
            "BubbleToParent", typeof(bool), typeof(MouseWheel),
            new PropertyMetadata(false, OnBubbleToParentChanged));

    public static bool GetBubbleToParent(DependencyObject o) => (bool)o.GetValue(BubbleToParentProperty);
    public static void SetBubbleToParent(DependencyObject o, bool value) => o.SetValue(BubbleToParentProperty, value);

    private static void OnBubbleToParentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element) return;
        if (e.NewValue is true) element.PreviewMouseWheel += OnPreviewMouseWheel;
        else                    element.PreviewMouseWheel -= OnPreviewMouseWheel;
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || sender is not UIElement element) return;

        // Marcamos el evento como manejado y lo re-disparamos sobre el padre visual: así burbujea
        // hasta el ScrollViewer del listado externo (las tablas internas muestran todas sus filas,
        // por lo que nunca necesitan scrollear por su cuenta).
        e.Handled = true;
        if (VisualTreeHelper.GetParent(element) is not UIElement parent) return;

        parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = element
        });
    }
}
