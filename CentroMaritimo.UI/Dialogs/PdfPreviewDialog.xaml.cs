using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using PuertoBB.Services.Common;

namespace CentroMaritimo.UI.Dialogs;

/// <summary>
/// Visor de PDF embebido (WebView2 / motor de Edge) mostrado en el overlay de MainWindow.
/// Si WebView2 no está disponible, ofrece abrir el PDF en el visor externo del sistema.
/// </summary>
public partial class PdfPreviewDialog : UserControl
{
    private readonly TaskCompletionSource<bool> _tcs = new();
    private readonly string _tempDir;
    private readonly string _tempPath;
    private Window? _win;
    private bool _cargado;
    private bool _dejarTemp;

    public Task<bool> Result => _tcs.Task;

    public PdfPreviewDialog(byte[] pdfBytes, string titulo, string? nombreArchivo = null)
    {
        InitializeComponent();
        TitleText.Text = titulo;

        // Subdir único + nombre lindo: así el botón "Guardar" del visor sugiere ese nombre
        // sin chocar con otras previsualizaciones abiertas.
        _tempDir = Path.Combine(Path.GetTempPath(), $"pbb_preview_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _tempPath = Path.Combine(_tempDir, $"{Formato.NombreArchivoSeguro(nombreArchivo)}.pdf");
        File.WriteAllBytes(_tempPath, pdfBytes);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_cargado) return;
        _cargado = true;

        // Tamaño grande relativo a la ventana; se readapta al redimensionar/maximizar.
        _win = Window.GetWindow(this);
        if (_win is not null)
        {
            AjustarTamano();
            _win.SizeChanged += OnWindowSizeChanged;
        }

        try
        {
            var userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Puerto de Bahia Blanca", "CentroMaritimo", "WebView2");
            Directory.CreateDirectory(userData);

            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userData);
            await Web.EnsureCoreWebView2Async(env);

            Web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            Web.CoreWebView2.Navigate(new Uri(_tempPath).AbsoluteUri);
        }
        catch
        {
            // WebView2 no disponible → ofrecer visor externo.
            Web.Visibility = Visibility.Collapsed;
            FallbackPanel.Visibility = Visibility.Visible;
        }
    }

    private void OpenExternal_Click(object sender, RoutedEventArgs e)
    {
        _dejarTemp = true; // el proceso externo necesita el archivo después de cerrar el overlay
        try { Process.Start(new ProcessStartInfo(_tempPath) { UseShellExecute = true }); }
        catch { /* nada que hacer */ }
        _tcs.TrySetResult(true);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => _tcs.TrySetResult(true);

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e) => AjustarTamano();

    private void AjustarTamano()
    {
        if (_win is null) return;
        Root.Width = Math.Max(Root.MinWidth, _win.ActualWidth - 120);
        Root.Height = Math.Max(Root.MinHeight, _win.ActualHeight - 120);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_win is not null) _win.SizeChanged -= OnWindowSizeChanged;
        try { Web.Dispose(); } catch { /* ya liberado */ }
        if (_dejarTemp) return;
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); } catch { /* lo limpia el SO */ }
    }
}
