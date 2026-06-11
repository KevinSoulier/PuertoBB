using System.Windows;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace CentroMaritimo.UI.Services;

/// <summary>
/// Acceso global al ISnackbarService cableado en MainWindow. Permite que ViewModels base
/// disparen toasts sin tener que recibir el servicio por inyección.
/// </summary>
public static class SnackbarHost
{
    public static ISnackbarService? Service { get; set; }

    public static void Exito(string mensaje) =>
        Mostrar(null, mensaje, ControlAppearance.Success, SymbolRegular.CheckmarkCircle24, TimeSpan.FromSeconds(4));

    public static void Advertencia(string mensaje) =>
        Mostrar(null, mensaje, ControlAppearance.Caution, SymbolRegular.Warning24, TimeSpan.FromSeconds(6));

    public static void Error(string mensaje) =>
        Mostrar(null, mensaje, ControlAppearance.Danger, SymbolRegular.ErrorCircle24, TimeSpan.FromSeconds(6));

    private static void Mostrar(string? title, string message, ControlAppearance appearance, SymbolRegular icon, TimeSpan timeout)
    {
        var svc = Service;
        if (svc?.GetSnackbarPresenter() is null) return;
        if (Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.Invoke(() => svc.Show(title!, message, appearance, new SymbolIcon(icon) { FontSize = 26, VerticalAlignment = VerticalAlignment.Center }, timeout));
        else
            svc.Show(title!, message, appearance, new SymbolIcon(icon) { FontSize = 26, VerticalAlignment = VerticalAlignment.Center }, timeout);
    }
}
