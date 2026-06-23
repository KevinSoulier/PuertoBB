using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace CentroMaritimo.UI.Behaviors;

/// <summary>
/// Behavior para <see cref="DataGrid"/>: agrega un menú de contexto "Copiar" que lleva el valor de la
/// celda clickeada (clic derecho) al portapapeles, sin tocar la selección por fila ni las acciones que
/// dependen de ella. Se activa con la propiedad adjunta <see cref="IsEnabledProperty"/>, aplicada vía
/// el estilo implícito de DataGrid en Styles.xaml. (Ctrl+C sigue copiando la fila como siempre.)
/// </summary>
public static class DataGridCellCopy
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(DataGridCellCopy), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject d, bool value) => d.SetValue(IsEnabledProperty, value);

    // Valor de la celda bajo el último clic derecho, que copia el item "Copiar".
    private static readonly DependencyProperty ValorClicProperty = DependencyProperty.RegisterAttached(
        "ValorClic", typeof(string), typeof(DataGridCellCopy), new PropertyMetadata(string.Empty));

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid) return;

        grid.PreviewMouseRightButtonDown -= OnRightButtonDown;
        if (e.NewValue is not true) return;

        grid.PreviewMouseRightButtonDown += OnRightButtonDown;
        if (grid.ContextMenu is not null) return; // respetar un menú propio de la grilla

        var copiar = new MenuItem { Header = "Copiar" };
        copiar.Click += (_, _) =>
        {
            var texto = (string)grid.GetValue(ValorClicProperty);
            if (!string.IsNullOrEmpty(texto))
                try { Clipboard.SetText(texto); } catch { /* portapapeles ocupado por otra app */ }
        };
        grid.ContextMenu = new ContextMenu { Items = { copiar } };
    }

    private static void OnRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var grid = (DataGrid)sender;
        var cell = BuscarAncestro<DataGridCell>(e.OriginalSource as DependencyObject);
        var valor = cell?.Column?.OnCopyingCellClipboardContent(cell.DataContext)?.ToString() ?? string.Empty;
        grid.SetValue(ValorClicProperty, valor);
    }

    private static T? BuscarAncestro<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d is not null and not T)
            d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        return d as T;
    }
}
