using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Afip.Documentos.Qr;

public record AfipQrPayload(
    long CuitEmisor,
    int PuntoVenta,
    int TipoComprobante,
    long NumeroComprobante,
    decimal Importe,
    int TipoDocReceptor,
    long NroDocReceptor,
    long CodAutorizacion,
    DateOnly FechaComprobante
)
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string BuildUrl()
    {
        var json = JsonSerializer.Serialize(new QrJson(
            ver: 1,
            fecha: FechaComprobante.ToString("yyyy-MM-dd"),
            cuit: CuitEmisor,
            ptoVta: PuntoVenta,
            tipoCmp: TipoComprobante,
            nroCmp: NumeroComprobante,
            importe: Importe,
            moneda: "PES",
            ctz: 1,
            tipoDocRec: TipoDocReceptor,
            nroDocRec: NroDocReceptor,
            tipoCodAut: "E",
            codAut: CodAutorizacion
        ), _opts);

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        return $"https://www.afip.gob.ar/fe/qr/?p={base64}";
    }

    private record QrJson(
        [property: JsonPropertyName("ver")] int ver,
        [property: JsonPropertyName("fecha")] string fecha,
        [property: JsonPropertyName("cuit")] long cuit,
        [property: JsonPropertyName("ptoVta")] int ptoVta,
        [property: JsonPropertyName("tipoCmp")] int tipoCmp,
        [property: JsonPropertyName("nroCmp")] long nroCmp,
        [property: JsonPropertyName("importe")] decimal importe,
        [property: JsonPropertyName("moneda")] string moneda,
        [property: JsonPropertyName("ctz")] int ctz,
        [property: JsonPropertyName("tipoDocRec")] int tipoDocRec,
        [property: JsonPropertyName("nroDocRec")] long nroDocRec,
        [property: JsonPropertyName("tipoCodAut")] string tipoCodAut,
        [property: JsonPropertyName("codAut")] long codAut
    );
}
