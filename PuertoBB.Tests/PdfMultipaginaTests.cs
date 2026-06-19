using Afip.Documentos;
using Afip.Documentos.Pdf;
using PdfSharp.Pdf.IO;
using QuestPDF.Infrastructure;
using Xunit;

namespace PuertoBB.Tests;

/// <summary>
/// P3-20 / N-11: un consolidado con muchos vouchers produce un PDF multipágina; el documento
/// debe seguir siendo válido y con el layout intacto (el pie de CAE/QR no debe romper el render).
/// </summary>
public class PdfMultipaginaTests
{
    static PdfMultipaginaTests() => QuestPDF.Settings.License = LicenseType.Community;

    [Fact]
    public void GenerarPdf_ConMuchosItems_ProduceMultipaginaValido()
    {
        var items = Enumerable.Range(1, 60)
            .Select(i => new ItemDocumento
            {
                Descripcion = $"Voucher {18200 + i} — Barco de prueba {i} — {(i % 28) + 1:00}/06/2026",
                Cantidad = 1,
                PrecioUnitario = 1000m,
                Subtotal = 1000m
            })
            .ToList();

        var doc = new ComprobanteDocumento
        {
            CodigoTipo = 15,
            NombreOverride = "RECIBO",
            PuntoVenta = 1,
            Numero = 200,
            FechaEmision = new DateTime(2026, 6, 11),
            Cae = "70417054367478",
            FechaVencimientoCae = new DateTime(2026, 6, 21),
            ImporteTotal = 60000m,
            Items = items,
            FechaVencimientoPago = new DateTime(2026, 7, 11),
            Emisor = new EmisorDocumento
            {
                RazonSocial = "Centro Marítimo de Bahía Blanca",
                Cuit = 30000000007L,
                CondicionIva = "IVA Exento",
                ColorAcentoHex = "#00695C"
            },
            Receptor = new ReceptorDocumento
            {
                RazonSocial = "Agencia de Prueba S.A.",
                TipoDocumento = 80,
                NroDocumento = 30000000001L
            }
        };

        var bytes = new AfipDocumentosService().GenerarPdf(doc);

        Assert.True(bytes.Length > 1000);
        using var ms = new MemoryStream(bytes);
        using var pdf = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
        Assert.True(pdf.PageCount >= 2, $"Se esperaba un PDF multipágina; tiene {pdf.PageCount} página(s).");
    }
}
