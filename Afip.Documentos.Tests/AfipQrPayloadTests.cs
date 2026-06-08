using System.Text;
using System.Text.Json;
using Afip.Documentos.Qr;
using Xunit;

namespace Afip.Documentos.Tests;

public class AfipQrPayloadTests
{
    private static AfipQrPayload BuildPayload() => new(
        CuitEmisor: 30000000007L,
        PuntoVenta: 1,
        TipoComprobante: 11,
        NumeroComprobante: 94,
        Importe: 12100.00m,
        TipoDocReceptor: 80,
        NroDocReceptor: 30000000001L,
        CodAutorizacion: 70417054367476L,
        FechaComprobante: new DateOnly(2026, 6, 7)
    );

    [Fact]
    public void BuildUrl_EmpienzaConUrlAfip()
    {
        var url = BuildPayload().BuildUrl();
        Assert.StartsWith("https://www.afip.gob.ar/fe/qr/?p=", url);
    }

    [Fact]
    public void BuildUrl_Base64DecodificaJsonValido()
    {
        var url = BuildPayload().BuildUrl();
        var base64 = url["https://www.afip.gob.ar/fe/qr/?p=".Length..];
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("ver").GetInt32());
        Assert.Equal("2026-06-07", root.GetProperty("fecha").GetString());
        Assert.Equal(30000000007L, root.GetProperty("cuit").GetInt64());
        Assert.Equal(1, root.GetProperty("ptoVta").GetInt32());
        Assert.Equal(11, root.GetProperty("tipoCmp").GetInt32());
        Assert.Equal(94L, root.GetProperty("nroCmp").GetInt64());
        Assert.Equal(12100.00m, root.GetProperty("importe").GetDecimal());
        Assert.Equal("PES", root.GetProperty("moneda").GetString());
        Assert.Equal(1, root.GetProperty("ctz").GetInt32());
        Assert.Equal(80, root.GetProperty("tipoDocRec").GetInt32());
        Assert.Equal(30000000001L, root.GetProperty("nroDocRec").GetInt64());
        Assert.Equal("E", root.GetProperty("tipoCodAut").GetString());
        Assert.Equal(70417054367476L, root.GetProperty("codAut").GetInt64());
    }

    [Fact]
    public void BuildUrl_ImporteUsaSeparadorDecimalPunto()
    {
        // Regresión: si se serializa con culture es-AR, importe sería "12100,00" y rompería el QR.
        var url = BuildPayload().BuildUrl();
        var base64 = url["https://www.afip.gob.ar/fe/qr/?p=".Length..];
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        Assert.Contains("12100", json);
        Assert.DoesNotContain("12100,", json);
    }
}
