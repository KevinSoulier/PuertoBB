using QuestPDF.Fluent;

namespace Afip.Documentos.Pdf;

public class AfipDocumentosService : IAfipDocumentosService
{
    public byte[] GenerarPdf(ComprobanteDocumento comprobante)
        => Document.Create(c => new ComprobanteTemplate(comprobante).Compose(c)).GeneratePdf();
}
