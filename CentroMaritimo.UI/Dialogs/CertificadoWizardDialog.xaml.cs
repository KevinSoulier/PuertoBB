using System.IO;
using System.Windows;
using System.Windows.Controls;
using Afip.Wsaa;
using Microsoft.Win32;
using PuertoBB.Core.Models;

namespace CentroMaritimo.UI.Dialogs;

/// <summary>
/// Asistente para generar un certificado AFIP nuevo: produce la clave privada + CSR con
/// <see cref="CsrGenerator"/>, ofrece guardarlos a disco y devuelve la clave para cargarla en el
/// punto de venta (modo CRT+KEY). El alias (CN) vive solo dentro de este diálogo.
/// </summary>
public partial class CertificadoWizardDialog : UserControl
{
    private readonly TaskCompletionSource<CertificadoWizardResult?> _tcs = new();
    public Task<CertificadoWizardResult?> Result => _tcs.Task;

    private readonly string _razonSocial;
    private readonly string _cuit;
    private readonly bool _usarHomologacion;

    private byte[]? _clavePem;
    private byte[]? _csrPem;
    private string _alias = string.Empty;

    public CertificadoWizardDialog(string razonSocial, string cuit, bool usarHomologacion)
    {
        InitializeComponent();
        _razonSocial = razonSocial;
        _cuit = new string((cuit ?? string.Empty).Where(char.IsDigit).ToArray());
        _usarHomologacion = usarHomologacion;
        AliasBox.Text = "puertobb";
    }

    private string Portal => _usarHomologacion
        ? "HOMOLOGACIÓN («WSASS - Autogestión Certificados Homologación»)"
        : "PRODUCCIÓN («Administración de Certificados Digitales»)";

    private void Generar_Click(object sender, RoutedEventArgs e)
    {
        OcultarError();
        var alias = (AliasBox.Text ?? string.Empty).Trim();
        if (_cuit.Length == 0) { MostrarError("Cargá el CUIT del emisor en Configuración antes de generar el certificado."); return; }
        if (string.IsNullOrWhiteSpace(_razonSocial)) { MostrarError("Cargá la razón social del emisor en Configuración antes de generar el certificado."); return; }
        if (CsrGenerator.ValidarAlias(alias) is { } errorAlias) { MostrarError(errorAlias); return; }

        try
        {
            var res = CsrGenerator.Generar(_cuit, _razonSocial, alias);
            _clavePem = res.ClavePrivadaPem;
            _csrPem = res.CsrPem;
            _alias = alias;

            EstadoText.Text = "Clave y CSR generados. Guardá el .csr (y, como respaldo, la .key) antes de continuar.";
            EstadoText.Visibility = Visibility.Visible;
            InstruccionesText.Text =
                $"Subí el .csr al portal de AFIP de {Portal} y descargá el .crt que te devuelvan. " +
                "Al cerrar este asistente, la clave queda cargada en el punto de venta (modo CRT+KEY): " +
                "guardá el punto y luego importá el .crt cuando lo tengas.";
            InstruccionesText.Visibility = Visibility.Visible;
            GuardarCsrButton.IsEnabled = true;
            GuardarKeyButton.IsEnabled = true;
            FinalizarButton.IsEnabled = true;
            GenerarButton.IsEnabled = false;
            AliasBox.IsEnabled = false;

            // Ofrecer guardar el CSR de inmediato: es el archivo que hay que subir a AFIP.
            GuardarBytes(_csrPem, $"{AliasArchivo()}.csr", "Pedido de certificado (*.csr)|*.csr|Todos|*.*", "CSR");
        }
        catch (Exception ex) { MostrarError($"No se pudo generar el certificado: {ex.Message}"); }
    }

    private void GuardarCsr_Click(object sender, RoutedEventArgs e)
        => GuardarBytes(_csrPem, $"{AliasArchivo()}.csr", "Pedido de certificado (*.csr)|*.csr|Todos|*.*", "CSR");

    private void GuardarKey_Click(object sender, RoutedEventArgs e)
        => GuardarBytes(_clavePem, $"{AliasArchivo()}.key", "Clave privada PEM (*.key)|*.key|Todos|*.*", "clave");

    private void Finalizar_Click(object sender, RoutedEventArgs e)
    {
        if (_clavePem is not { Length: > 0 }) { MostrarError("Generá la clave y el CSR antes de continuar."); return; }
        _tcs.TrySetResult(new CertificadoWizardResult(_clavePem, _alias));
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => _tcs.TrySetResult(null);

    private string AliasArchivo()
    {
        var a = string.IsNullOrWhiteSpace(_alias) ? "certificado" : _alias;
        return string.Concat(a.Split(Path.GetInvalidFileNameChars()));
    }

    private void GuardarBytes(byte[]? datos, string nombreSugerido, string filtro, string queCosa)
    {
        if (datos is not { Length: > 0 }) { MostrarError($"No hay {queCosa} para guardar."); return; }
        var dlg = new SaveFileDialog { FileName = nombreSugerido, Filter = filtro };
        if (dlg.ShowDialog() != true) return;
        try { File.WriteAllBytes(dlg.FileName, datos); OcultarError(); }
        catch (Exception ex) { MostrarError($"No se pudo guardar: {ex.Message}"); }
    }

    private void MostrarError(string msg) { ErrorText.Text = msg; ErrorText.Visibility = Visibility.Visible; }
    private void OcultarError() => ErrorText.Visibility = Visibility.Collapsed;
}
