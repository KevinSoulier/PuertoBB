using System.Text;
using Afip.Documentos.Pdf;
using QuestPDF.Infrastructure;
using Xunit;

namespace Afip.Documentos.Tests;

public class AfipDocumentosServiceTests
{
    static AfipDocumentosServiceTests() => QuestPDF.Settings.License = LicenseType.Community;

    private static ComprobanteDocumento BuildRecibo(bool conItems = false)
    {
        var items = conItems
            ? new[]
            {
                new ItemDocumento { Descripcion = "Voucher 18265 — Walsh B — 05/06/2026", PrecioUnitario = 45000m, Subtotal = 45000m },
                new ItemDocumento { Descripcion = "Voucher 18266 — Condor I — 06/06/2026", PrecioUnitario = 30000m, Subtotal = 30000m }
            }
            : Array.Empty<ItemDocumento>();

        return new ComprobanteDocumento
        {
            CodigoTipo = 15,   // Recibo C
            PuntoVenta = 1,
            Numero = 94,
            FechaEmision = new DateTime(2026, 6, 7),
            Cae = "70417054367476",
            FechaVencimientoCae = new DateTime(2026, 6, 17),
            ImporteTotal = conItems ? 75000m : 45000m,
            ConceptoGeneral = conItems ? null : "Cuota mensual Junio 2026",
            Items = items,
            FechaVencimientoPago = new DateTime(2026, 6, 30),
            Emisor = new EmisorDocumento
            {
                RazonSocial = "Cámara Portuaria de Bahía Blanca",
                Cuit = 30000000007L,
                CondicionIva = "IVA Exento",
                ColorAcentoHex = "#1565C0"
            },
            Receptor = new ReceptorDocumento
            {
                RazonSocial = "Empresa de Prueba S.A.",
                TipoDocumento = 80,
                NroDocumento = 30000000001L
            }
        };
    }

    private static bool EsPdf(byte[] bytes)
        => bytes.Length >= 4 && Encoding.ASCII.GetString(bytes, 0, 4) == "%PDF";

    [Fact]
    public void GenerarPdf_ReciboSinItems_DevuelvePdfValido()
    {
        var svc = new AfipDocumentosService();
        var bytes = svc.GenerarPdf(BuildRecibo(conItems: false));

        Assert.True(bytes.Length > 1000);
        Assert.True(EsPdf(bytes));
    }

    [Fact]
    public void GenerarPdf_ReciboConItems_DevuelvePdfValido()
    {
        var svc = new AfipDocumentosService();
        var bytes = svc.GenerarPdf(BuildRecibo(conItems: true));

        Assert.True(bytes.Length > 1000);
        Assert.True(EsPdf(bytes));
    }

    [Fact]
    public void GenerarPdf_SinVencimientoCae_DevuelvePdfValidoSinFechaMinima()
    {
        // N-5: si AFIP no devolvió vencimiento (default), el PDF se genera igual y el template
        // omite la línea (no imprime "01/01/0001").
        var svc = new AfipDocumentosService();
        var bytes = svc.GenerarPdf(BuildRecibo(conItems: true) with { FechaVencimientoCae = default });

        Assert.True(bytes.Length > 1000);
        Assert.True(EsPdf(bytes));
    }

    [Fact]
    public void GenerarPdf_NotaDeCreditoConComprobanteAsociado_DevuelvePdfValido()
    {
        var nc = new ComprobanteDocumento
        {
            CodigoTipo = 13,   // NC C
            PuntoVenta = 1,
            Numero = 5,
            FechaEmision = new DateTime(2026, 6, 10),
            Cae = "70417054367477",
            FechaVencimientoCae = new DateTime(2026, 6, 20),
            ImporteTotal = 45000m,
            ConceptoGeneral = "Anulación recibo 0001-00000094",
            ComprobanteAsociado = new ComprobanteAsociado(CodigoTipo: 15, PuntoVenta: 1, Numero: 94),
            Emisor = new EmisorDocumento
            {
                RazonSocial = "Cámara Portuaria de Bahía Blanca",
                Cuit = 30000000007L,
                CondicionIva = "IVA Exento"
            },
            Receptor = new ReceptorDocumento
            {
                RazonSocial = "Empresa de Prueba S.A.",
                TipoDocumento = 80,
                NroDocumento = 30000000001L
            }
        };

        var svc = new AfipDocumentosService();
        var bytes = svc.GenerarPdf(nc);

        Assert.True(bytes.Length > 1000);
        Assert.True(EsPdf(bytes));
    }
}
