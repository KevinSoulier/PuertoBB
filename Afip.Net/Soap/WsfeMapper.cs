using System.Globalization;
using Afip.Abstractions;
using Afip.Soap.Wsfe;

namespace Afip.Soap;

/// <summary>
/// Mapea los modelos neutros de PuertoBB ↔ contratos SOAP de WSFE.
/// Público para poder testear el mapeo sin invocar al webservice real.
/// Regla crítica: para tipo C (exento IVA) NO se setea el array <c>Iva</c> (si se incluye, error 10071).
/// </summary>
public static class WsfeMapper
{
    public static FECAERequest ToFECAERequest(WsfeCaeRequest req)
    {
        // P3-7: redondear ANTES del cast a double para evitar artefactos binarios en los importes.
        var total = (double)Math.Round(req.ImporteTotal, 2, MidpointRounding.AwayFromZero);
        var det = new FECAEDetRequest
        {
            Concepto = req.Concepto,
            DocTipo = req.DocTipo,
            DocNro = req.DocNro,
            CbteDesde = req.Numero,
            CbteHasta = req.Numero,
            CbteFch = req.FechaComprobante.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            CondicionIVAReceptorId = req.CondicionIvaReceptorId, // RG 5616: obligatorio; 0 = rechazo 10242
            ImpTotal = total,
            ImpTotConc = 0,
            ImpNeto = total,                    // tipo C: el total va en Neto (no se discrimina IVA)
            ImpOpEx = 0,                        // tipo C: importe exento SIEMPRE 0 (rechazo 10044 si no)
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

    /// <summary>
    /// Mapea la respuesta de FECompConsultar. Null si el comprobante no existe (error 602);
    /// otros errores se lanzan (el caller decide).
    /// </summary>
    public static WsfeComprobanteConsultado? ToComprobanteConsultado(FECompConsultaResponse resp)
    {
        if (resp.ResultGet is not { } det)
        {
            // 602 = "No existen datos en nuestros registros para los parametros ingresados".
            if (resp.Errors is { Length: > 0 } && resp.Errors.All(e => e.Code == 602))
                return null;
            var msg = resp.Errors is { Length: > 0 }
                ? string.Join(" · ", resp.Errors.Select(e => $"[{e.Code}] {e.Msg}"))
                : "FECompConsultar no devolvió datos.";
            throw new InvalidOperationException(msg);
        }

        DateTime? vto = null;
        if (!string.IsNullOrWhiteSpace(det.FchVto) &&
            DateTime.TryParseExact(det.FchVto, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fv))
            vto = fv;
        var fecha = DateTime.TryParseExact(det.CbteFch, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fc)
            ? fc : default;

        return new WsfeComprobanteConsultado
        {
            Numero = det.CbteDesde,
            ImporteTotal = (decimal)det.ImpTotal,
            DocNro = det.DocNro,
            FechaComprobante = fecha,
            Cae = det.CodAutorizacion,
            FechaVencimientoCae = vto
        };
    }

    /// <summary>Mapea FEParamGetPtosVenta. Lista vacía si AFIP no informa puntos (habitual en homologación).</summary>
    public static IReadOnlyList<WsfePuntoVenta> ToPuntosVenta(FEPtoVentaResponse resp)
    {
        var items = ResultadoOVacio(resp.ResultGet, resp.Errors);
        return items.Select(p => new WsfePuntoVenta
        {
            Numero = p.Nro,
            EmisionTipo = p.EmisionTipo,
            Bloqueado = EsSi(p.Bloqueado),
            FechaBaja = ParseFecha(p.FchBaja)
        }).ToList();
    }

    /// <summary>Mapea FEParamGetTiposCbte.</summary>
    public static IReadOnlyList<WsfeTipoComprobante> ToTiposComprobante(CbteTipoResponse resp)
    {
        var items = ResultadoOVacio(resp.ResultGet, resp.Errors);
        return items.Select(t => new WsfeTipoComprobante
        {
            Id = t.Id,
            Descripcion = t.Desc,
            VigenteDesde = ParseFecha(t.FchDesde),
            VigenteHasta = ParseFecha(t.FchHasta)
        }).ToList();
    }

    /// <summary>Mapea FEParamGetCondicionIvaReceptor (RG 5616).</summary>
    public static IReadOnlyList<WsfeCondicionIvaReceptor> ToCondicionesIvaReceptor(CondicionIvaReceptorResponse resp)
    {
        var items = ResultadoOVacio(resp.ResultGet, resp.Errors);
        return items.Select(c => new WsfeCondicionIvaReceptor
        {
            Id = c.Id,
            Descripcion = c.Desc,
            ClaseComprobante = c.Cmp_Clase
        }).ToList();
    }

    /// <summary>
    /// Criterio común de las tablas FEParamGet*: sin resultados + sin errores (o solo 602 "sin datos")
    /// → lista vacía; cualquier otro error → excepción con "[código] mensaje" (el caller decide).
    /// </summary>
    private static T[] ResultadoOVacio<T>(T[]? resultGet, Err[]? errors)
    {
        if (resultGet is { Length: > 0 }) return resultGet;
        if (errors is { Length: > 0 } && errors.Any(e => e.Code != 602))
            throw new InvalidOperationException(string.Join(" · ", errors.Select(e => $"[{e.Code}] {e.Msg}")));
        return [];
    }

    /// <summary>"S"/"N" de AFIP → bool (cualquier cosa que no sea "S" cuenta como no).</summary>
    private static bool EsSi(string? valor) => string.Equals(valor, "S", StringComparison.OrdinalIgnoreCase);

    /// <summary>Fecha yyyyMMdd de AFIP → DateTime?; "NULL", vacío o formato inesperado → null.</summary>
    private static DateTime? ParseFecha(string? valor)
        => DateTime.TryParseExact(valor, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f)
            ? f : null;

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
