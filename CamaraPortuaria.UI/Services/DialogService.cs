using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CamaraPortuaria.UI.Dialogs;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Services.Common;

namespace CamaraPortuaria.UI.Services;

/// <summary>
/// Implementa IDialogService mostrando UserControls en un overlay de MainWindow.
/// Único punto de diálogos modales — reemplaza a MessageBox.
/// </summary>
public class DialogService : IDialogService
{
    private Grid? _overlay;
    private ContentPresenter? _host;

    public void Initialize(Grid overlay, ContentPresenter host)
    {
        _overlay = overlay;
        _host = host;
    }

    public Task<bool> ShowConfirmAsync(string title, string message, string confirmText = "Confirmar", string cancelText = "Cancelar")
    {
        var dialog = new ConfirmDialog(title, message, confirmText, cancelText);
        return ShowAsync(dialog, dialog.Result);
    }

    public async Task ShowAlertAsync(string title, string message, string closeText = "Aceptar")
    {
        var dialog = new AlertDialog(title, message, closeText);
        await ShowAsync(dialog, dialog.Result.ContinueWith(_ => true));
    }

    public Task<string?> ShowInputAsync(string title, string placeholder, string? initialValue = null)
    {
        var dialog = new InputDialog(title, placeholder, initialValue);
        return ShowAsync(dialog, dialog.Result);
    }

    public async Task ShowPdfAsync(byte[] pdfBytes, string titulo, string? nombreArchivo = null)
    {
        // Cámara Portuaria aún no tiene visor embebido: abre el PDF en el visor externo del sistema.
        // Subdir único para que el archivo conserve el nombre lindo sin chocar con otros.
        var carpeta = Path.Combine(Path.GetTempPath(), $"pbb_preview_{Guid.NewGuid():N}");
        Directory.CreateDirectory(carpeta);
        var ruta = Path.Combine(carpeta, $"{Formato.NombreArchivoSeguro(nombreArchivo)}.pdf");
        await File.WriteAllBytesAsync(ruta, pdfBytes);
        Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });
    }

    private async Task<T> ShowAsync<T>(UIElement dialog, Task<T> resultTask)
    {
        if (_overlay is null || _host is null)
            throw new InvalidOperationException("DialogService no fue inicializado con el overlay de MainWindow.");

        _host.Content = dialog;
        _overlay.Visibility = Visibility.Visible;
        try
        {
            return await resultTask;
        }
        finally
        {
            _overlay.Visibility = Visibility.Collapsed;
            _host.Content = null;
        }
    }
}
