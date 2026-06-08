namespace Afip.Documentos.Pdf;

public interface IAfipDocumentosService
{
    /// <summary>Genera el PDF del comprobante AFIP (fiscalmente válido: incluye CAE y QR). Síncrono — QuestPDF no hace IO.</summary>
    byte[] GenerarPdf(ComprobanteDocumento comprobante);
}
