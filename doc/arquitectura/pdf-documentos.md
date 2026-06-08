# Generación y previsualización de PDFs

## Stack

| Librería | Uso |
|---|---|
| `QuestPDF` | Generación de PDFs (layout fluent, sin licencia para uso community) |
| `PdfSharp` | Merge de múltiples PDFs en uno |
| `QRCoder` | Código QR AFIP embebido en el comprobante |

## Módulo `Afip.Documentos` (proyecto interno)

Genera comprobantes fiscales AFIP-compliant (CAE + QR).

```
Afip.Documentos/
├── Pdf/
│   ├── IAfipDocumentosService.cs   → byte[] GenerarPdf(ComprobanteDocumento)
│   ├── AfipDocumentosService.cs    → implementación
│   └── ComprobanteTemplate.cs      → layout QuestPDF (IDocument)
├── Models/
│   ├── ComprobanteDocumento.cs     → record con todos los datos del comprobante
│   ├── EmisorDocumento.cs          → razón social, CUIT, logo, color acento
│   └── ReceptorDocumento.cs        → CUIT/DNI, razón social, domicilio
└── Qr/
    └── AfipQrBuilder.cs            → genera PNG del QR AFIP sin System.Drawing
```

Registro DI: `services.AddAfipDocumentos()` — llamado automáticamente por `AddPuertoBBPdf()`.

## Servicios por aplicación

En `PuertoBB.Services/Pdf/`:

```csharp
// Cámara Portuaria
ICamaraPortuariaPdfService
    Task<byte[]> GenerarPdfReciboAsync(CamaraPortuaria.Recibo recibo, ...)
    Task<byte[]> GenerarPdfNotaDeCreditoAsync(CamaraPortuaria.NotaDeCredito nc, ...)

// Centro Marítimo
ICentroMaritimoPdfService
    Task<byte[]> GenerarPdfVoucherAsync(CentroMaritimo.Voucher voucher, ...)
    Task<byte[]> GenerarPdfReciboAsync(CentroMaritimo.Recibo recibo, ...)
    Task<byte[]> GenerarPdfConsolidadoAsync(CentroMaritimo.Recibo recibo, IEnumerable<Voucher>, ...)
    Task<byte[]> GenerarPdfDescargaAsync(IReadOnlyList<Voucher>, CentroMaritimo.Recibo?, ...)

// Merge de PDFs
IPdfMerger
    byte[] Merge(IEnumerable<byte[]> pdfs)
```

**Registro DI:** `services.AddPuertoBBPdf()` en `App.xaml.cs` de cada app.
Ya registrado en ambas apps — no hace falta agregar nada.

## Previsualización en la UI

### CamaraPortuaria.UI — visor externo del sistema

`IDialogService.ShowPdfAsync(byte[] pdfBytes, string titulo, string? nombreArchivo)` escribe el PDF
en un directorio temporal único y lo abre con el visor del sistema (`Process.Start + UseShellExecute`).

### CentroMaritimo.UI — visor embebido (WebView2)

`PdfPreviewDialog` en `CentroMaritimo.UI/Dialogs/` muestra el PDF dentro de la ventana usando WebView2
(motor de Edge). Si WebView2 no está disponible, cae al visor externo como fallback.
`DialogService.ShowPdfAsync()` lo muestra en el overlay de MainWindow.

## Patrón para agregar preview a un ViewModel (receta)

```csharp
// 1. Inyectar el servicio PDF en el constructor
public MiViewModel(
    ...,
    ICamaraPortuariaPdfService pdf)   // agregar este parámetro
{
    _pdf = pdf;
    PrevisualizarCommand = new AsyncRelayCommand(PrevisualizarAsync, () => Seleccionado is not null);
}

// 2. Implementar el comando
private async Task PrevisualizarAsync()
{
    if (Seleccionado is null) return;
    var entidad = await _repo.GetConDetalleAsync(Seleccionado.Id);  // carga navegación
    if (entidad is null) { MostrarError("No se encontró."); return; }
    IsBusy = true;
    try
    {
        var bytes = await _pdf.GenerarPdfXxxAsync(entidad);
        await _dialog.ShowPdfAsync(bytes, $"Título {Seleccionado.Id}", $"NombreArchivo_{Seleccionado.Id}");
    }
    catch (Exception ex) { MostrarError($"No se pudo previsualizar: {ex.Message}"); }
    finally { IsBusy = false; }
}
```

```xml
<!-- 3. Botón en el XAML -->
<ui:Button Content="Previsualizar" Command="{Binding PrevisualizarCommand}"
           Style="{StaticResource AccionIconButton}"
           ToolTip="Abre el PDF en el visor del sistema.">
    <ui:Button.Icon><ui:SymbolIcon Symbol="DocumentPdf24" /></ui:Button.Icon>
</ui:Button>
```

**Notas:**
- `GetConDetalleAsync(id)` carga las propiedades de navegación (Empresa, etc.) necesarias para el PDF.
- `IsBusy = true` durante la generación muestra el spinner y deshabilita controles.
- No hay `try/catch` en la generación del PDF dentro de servicios — las excepciones llegan al ViewModel.
