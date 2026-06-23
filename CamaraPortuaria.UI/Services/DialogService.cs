using System.Windows;
using System.Windows.Controls;
using CamaraPortuaria.UI.Dialogs;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models;

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

    public Task<string?> ShowInputAsync(string title, string placeholder, string? initialValue = null, string? description = null)
    {
        var dialog = new InputDialog(title, placeholder, initialValue, description);
        return ShowAsync(dialog, dialog.Result);
    }

    public Task ShowPdfAsync(byte[] pdfBytes, string titulo, string? nombreArchivo = null)
    {
        var dialog = new PdfPreviewDialog(pdfBytes, titulo, nombreArchivo);
        return ShowAsync(dialog, dialog.Result);
    }

    public Task<EmisionIndividualResult?> ShowEmisionIndividualAsync(
        string labelCliente,
        IReadOnlyList<ClienteEmisionItem> entidades,
        IReadOnlyList<string> conceptos)
    {
        var dialog = new EmisionIndividualDialog(labelCliente, entidades, conceptos);
        return ShowAsync(dialog, dialog.Result);
    }

    public Task<IReadOnlyList<ReciboLineaInput>?> ShowEditarReciboAsync(
        IReadOnlyList<ReciboLineaInput> lineasActuales,
        IReadOnlyList<string> conceptos)
    {
        var dialog = new EditarReciboDialog(lineasActuales, conceptos);
        return ShowAsync(dialog, dialog.Result);
    }

    public Task<CertificadoWizardResult?> ShowCertificadoWizardAsync(string razonSocial, string cuit, bool usarHomologacion)
    {
        var dialog = new CertificadoWizardDialog(razonSocial, cuit, usarHomologacion);
        return ShowAsync(dialog, dialog.Result);
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
