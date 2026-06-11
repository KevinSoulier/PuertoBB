using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CentroMaritimo.UI.ViewModels.Items;
using PuertoBB.Core.Models;
using PuertoBB.Services.Common;

namespace CentroMaritimo.UI.Dialogs;

public partial class EmisionIndividualDialog : UserControl
{
    private readonly TaskCompletionSource<EmisionIndividualResult?> _tcs = new();
    public Task<EmisionIndividualResult?> Result => _tcs.Task;

    private readonly ObservableCollection<LineaEmisionItem> _lineas = [];
    private readonly List<string> _todosConceptos;
    private readonly ObservableCollection<string> _conceptosFiltrados = [];

    public EmisionIndividualDialog(
        string labelEntidad,
        IReadOnlyList<EntidadEmisionItem> entidades,
        IReadOnlyList<string> conceptos)
    {
        InitializeComponent();

        _todosConceptos = [.. conceptos];

        LabelEntidad.Text = labelEntidad;
        EntidadCombo.ItemsSource = entidades;
        FechaPicker.SelectedDate = DateTime.Today;

        LineasGrid.ItemsSource = _lineas;
        DetalleCombo.ItemsSource = _conceptosFiltrados;
        CantidadBox.Value = 1;
        PrecioBox.Value = 0;

        ActualizarConceptosFiltrados(string.Empty);
        ActualizarTotal();

        DetalleCombo.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(DetalleCombo_TextChanged));
    }

    private void ActualizarConceptosFiltrados(string texto)
    {
        _conceptosFiltrados.Clear();
        var lista = string.IsNullOrEmpty(texto)
            ? _todosConceptos
            : _todosConceptos.Where(c => c.Contains(texto, StringComparison.OrdinalIgnoreCase));
        foreach (var c in lista) _conceptosFiltrados.Add(c);
    }

    private void ActualizarTotal()
    {
        var total = _lineas.Sum(l => l.Importe);
        TotalText.Text = $"Total: {Formato.Moneda(total)}";
    }

    private void DetalleCombo_TextChanged(object sender, TextChangedEventArgs e)
    {
        ActualizarConceptosFiltrados(DetalleCombo.Text ?? string.Empty);
    }

    private void AgregarLinea_Click(object sender, RoutedEventArgs e)
    {
        var desc = (DetalleCombo.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(desc)) { MostrarError("Ingrese un detalle para la línea."); return; }

        var cantidad = (decimal)(CantidadBox.Value ?? 0);
        if (cantidad <= 0) { MostrarError("La cantidad debe ser mayor a cero."); return; }

        var precio = (decimal)(PrecioBox.Value ?? 0);
        if (precio <= 0) { MostrarError("El precio unitario debe ser mayor a cero."); return; }

        _lineas.Add(new LineaEmisionItem(desc, cantidad, precio));
        ActualizarTotal();
        OcultarError();

        DetalleCombo.Text = string.Empty;
        CantidadBox.Value = 1;
        PrecioBox.Value = 0;
    }

    private void QuitarLinea_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is LineaEmisionItem linea)
        {
            _lineas.Remove(linea);
            ActualizarTotal();
        }
    }

    private void TryConfirmar(bool enviarMail)
    {
        if (EntidadCombo.SelectedItem is not EntidadEmisionItem entidad)
        { MostrarError("Seleccione una agencia."); return; }

        if (FechaPicker.SelectedDate is null || FechaPicker.SelectedDate.Value.Date > DateTime.Today)
        { MostrarError("La fecha de emisión no puede ser futura."); return; }

        if (_lineas.Count == 0)
        { MostrarError("Agregue al menos un ítem al recibo."); return; }

        var lineas = _lineas.Select(l => new ReciboLineaInput(l.Descripcion, l.Cantidad, l.PrecioUnitario)).ToList();
        _tcs.TrySetResult(new EmisionIndividualResult(entidad.Id, FechaPicker.SelectedDate.Value, lineas, enviarMail));
    }

    private void Emitir_Click(object sender, RoutedEventArgs e) => TryConfirmar(enviarMail: false);
    private void EmitirYEnviar_Click(object sender, RoutedEventArgs e) => TryConfirmar(enviarMail: true);
    private void Cancelar_Click(object sender, RoutedEventArgs e) => _tcs.TrySetResult(null);

    private void MostrarError(string msg) { ErrorText.Text = msg; ErrorText.Visibility = Visibility.Visible; }
    private void OcultarError() => ErrorText.Visibility = Visibility.Collapsed;

    private void ComboBox_OpenOnDownKey(object sender, KeyEventArgs e)
    {
        if (sender is not ComboBox cb || cb.IsDropDownOpen) return;

        if (e.Key == Key.Down)
        {
            cb.IsDropDownOpen = true;
            e.Handled = true;
            return;
        }

        if (EsTeclaImprimible(e.Key) && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == 0)
            cb.IsDropDownOpen = true;
    }

    private static bool EsTeclaImprimible(Key k) =>
        (k >= Key.A && k <= Key.Z) ||
        (k >= Key.D0 && k <= Key.D9) ||
        (k >= Key.NumPad0 && k <= Key.NumPad9) ||
        k is Key.OemMinus or Key.OemPlus or Key.OemComma or Key.OemPeriod
          or Key.OemQuestion or Key.OemSemicolon or Key.OemQuotes or Key.Space;
}
