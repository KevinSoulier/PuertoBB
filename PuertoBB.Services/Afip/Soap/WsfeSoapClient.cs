using System.Globalization;
using System.ServiceModel;
using PuertoBB.Services.Afip.Abstractions;
using PuertoBB.Services.Afip.Soap.Wsfe;

namespace PuertoBB.Services.Afip.Soap;

/// <summary>
/// Implementación real de <see cref="IWsfeClient"/> sobre el cliente SOAP generado (WSFE v1).
/// Mapea los modelos neutros de PuertoBB a los contratos de AFIP.
/// Regla crítica: para comprobantes tipo C (exento IVA) NO se envía el array <c>Iva</c>.
/// </summary>
public class WsfeSoapClient : IWsfeClient
{
    private const string UrlHomologacion = "https://wswhomo.afip.gov.ar/wsfev1/service.asmx";
    private const string UrlProduccion   = "https://servicios1.afip.gov.ar/wsfev1/service.asmx";

    private static ServiceSoapClient Crear(bool homologacion)
    {
        var url = homologacion ? UrlHomologacion : UrlProduccion;
        return new ServiceSoapClient(ServiceSoapClient.EndpointConfiguration.ServiceSoap, new EndpointAddress(url));
    }

    private static FEAuthRequest Auth(string token, string sign, string cuit)
        => new() { Token = token, Sign = sign, Cuit = long.Parse(cuit) };

    public async Task<bool> DummyAsync(bool usarHomologacion, CancellationToken ct = default)
    {
        var client = Crear(usarHomologacion);
        try
        {
            var r = await client.FEDummyAsync();
            await client.CloseAsync();
            var d = r.Body.FEDummyResult;
            return string.Equals(d.AppServer, "OK", StringComparison.OrdinalIgnoreCase)
                && string.Equals(d.AuthServer, "OK", StringComparison.OrdinalIgnoreCase)
                && string.Equals(d.DbServer, "OK", StringComparison.OrdinalIgnoreCase);
        }
        catch { client.Abort(); throw; }
    }

    public async Task<long> UltimoComprobanteAsync(string token, string sign, string cuit, int puntoVenta, int tipoComprobante, bool usarHomologacion, CancellationToken ct = default)
    {
        var client = Crear(usarHomologacion);
        try
        {
            var r = await client.FECompUltimoAutorizadoAsync(Auth(token, sign, cuit), puntoVenta, tipoComprobante);
            await client.CloseAsync();
            return r.Body.FECompUltimoAutorizadoResult.CbteNro;
        }
        catch { client.Abort(); throw; }
    }

    public async Task<WsfeCaeResponse> SolicitarCaeAsync(string token, string sign, string cuit, WsfeCaeRequest request, bool usarHomologacion, CancellationToken ct = default)
    {
        var client = Crear(usarHomologacion);
        try
        {
            var feReq = WsfeMapper.ToFECAERequest(request);
            var r = await client.FECAESolicitarAsync(Auth(token, sign, cuit), feReq);
            await client.CloseAsync();
            return WsfeMapper.ToWsfeCaeResponse(r.Body.FECAESolicitarResult, request.Numero);
        }
        catch { client.Abort(); throw; }
    }
}
