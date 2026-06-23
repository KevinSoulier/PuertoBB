using System.Text;
using Afip.Documentos.Pdf;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Services.Pdf;
using QuestPDF.Infrastructure;
using Xunit;

namespace PuertoBB.Tests;

public class VoucherPdfTests
{
    static VoucherPdfTests() => QuestPDF.Settings.License = LicenseType.Community;

    private static Voucher BuildVoucher() => new()
    {
        Numero = 18265,
        Importe = 45000m,
        Fecha = new DateTime(2026, 6, 5),
        ClienteId = 1,
        BarcoId = 1,
        Barco = new Barco { Nombre = "Walsh B" }
    };

    private static CentroMaritimoPdfService BuildService() =>
        new(new PdfMerger(), new AfipDocumentosService(), new FakeAfipConfigProvider());

    private static bool EsPdf(byte[] bytes) =>
        bytes.Length >= 4 && Encoding.ASCII.GetString(bytes, 0, 4) == "%PDF";

    [Fact]
    public async Task GenerarPdfVoucher_DevuelvePdfValido()
    {
        var pdf = BuildService();

        var bytes = await pdf.GenerarPdfVoucherAsync(BuildVoucher());

        Assert.True(bytes.Length > 1000);
        Assert.True(EsPdf(bytes));
    }
}
