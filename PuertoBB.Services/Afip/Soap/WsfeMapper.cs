using System.Globalization;
using PuertoBB.Services.Afip.Abstractions;
using PuertoBB.Services.Afip.Soap.Wsfe;

namespace PuertoBB.Services.Afip.Soap;

/// <summary>
/// Mapea los modelos neutros de PuertoBB ↔ contratos SOAP de WSFE.
/// Público para poder testear el mapeo sin invocar al webservice real.
/// Regla crítica: para tipo C (exento IVA) NO se setea el array <c>Iva</c> (si se incluye, error 10071).
/// </summary>
public static class WsfeMapper
{
    public static FECAERequest ToFECAERequest(WsfeCaeRequest req)
    {
        var det = new FECAEDetRequest
        {
            Concepto = req.Concepto,
            DocTipo = req.DocTipo,
            DocNro = req.DocNro,
            CbteDesde = req.Numero,
            CbteHasta = req.Numero,
            CbteFch = req.FechaComprobante.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            ImpTotal = (double)req.ImporteTotal,
            ImpTotConc = 0,
            ImpNeto = 0,                        // todo exento para tipo C
            ImpOpEx = (double)req.ImporteTotal,
            ImpTrib = 0,
            ImpIVA = 0,
            MonId = "PES",
            MonCotiz = 1,
            FchServDesde = req.ServicioDesde.ToString(CultureInfo.InvariantCulture),
            FchServHasta = req.ServicioHasta.ToString(CultureInfo.InvariantCulture),
            FchVtoPago = req.VencimientoPago.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
        };

        if (req.ComprobanteAsociado is { } a)
            det.CbtesAsoc = [new CbteAsoc { Tipo = a.Tipo, PtoVta = a.PuntoDeVenta, Nro = a.Numero, Cuit = a.Cuit.ToString(CultureInfo.InvariantCulture) }];

        return new FECAERequest
        {
            FeCabReq = new FECAECabRequest { CantReg = 1, PtoVta = req.PuntoDeVenta, CbteTipo = req.TipoComprobante },
            FeDetReq = [det]
        };
    }

    public static WsfeCaeResponse ToWsfeCaeResponse(FECAEResponse resp, long numeroSolicitado)
    {
        var aprobado = string.Equals(resp.FeCabResp?.Resultado, "A", StringComparison.OrdinalIgnoreCase);
        var det = resp.FeDetResp?.FirstOrDefault();

        var observaciones = new List<string>();
        if (resp.Errors is { Length: > 0 })
            observaciones.AddRange(resp.Errors.Select(e => $"[{e.Code}] {e.Msg}"));
        if (det?.Observaciones is { Length: > 0 })
            observaciones.AddRange(det.Observaciones.Select(o => $"[{o.Code}] {o.Msg}"));

        DateTime? vto = null;
        if (!string.IsNullOrWhiteSpace(det?.CAEFchVto) &&
            DateTime.TryParseExact(det!.CAEFchVto, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fv))
            vto = fv;

        return new WsfeCaeResponse
        {
            Aprobado = aprobado && !string.IsNullOrWhiteSpace(det?.CAE),
            Cae = det?.CAE,
            FechaVencimientoCae = vto,
            Numero = det?.CbteDesde ?? numeroSolicitado,
            Observaciones = observaciones.Count > 0 ? string.Join(" · ", observaciones) : null
        };
    }
}
