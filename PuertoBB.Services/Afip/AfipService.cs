using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Common;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Afip;
using PuertoBB.Services.Afip.Abstractions;

namespace PuertoBB.Services.Afip;

/// <summary>
/// Servicio AFIP real. Orquesta WSAA (TRA → CMS → ticket cacheado) + WSFE (último número → CAE).
/// Depende de IWsaaClient / IWsfeClient (clientes SOAP a generar desde WSDL).
/// </summary>
public class AfipService : IAfipService
{
    private readonly IWsaaClient _wsaa;
    private readonly IWsfeClient _wsfe;
    private readonly IAfipConfigProvider _configProvider;
    private readonly WsaaTokenCache _tokenCache;
    private readonly ILogger<AfipService> _logger;

    public AfipService(
        IWsaaClient wsaa,
        IWsfeClient wsfe,
        IAfipConfigProvider configProvider,
        WsaaTokenCache tokenCache,
        ILogger<AfipService> logger)
    {
        _wsaa = wsaa;
        _wsfe = wsfe;
        _configProvider = configProvider;
        _tokenCache = tokenCache;
        _logger = logger;
    }

    public async Task<ServiceResult<CaeResult>> ObtenerCAEAsync(ComprobanteAfipRequest request, CancellationToken ct = default)
    {
        try
        {
            var config = await _configProvider.GetAsync(ct);
            if (string.IsNullOrWhiteSpace(config.CertificadoRuta) || !File.Exists(config.CertificadoRuta))
                return ServiceResult<CaeResult>.Fail(
                    "No hay certificado AFIP configurado. Cargue el certificado en Configuración o use el modo de prueba.");

            var (token, sign) = await ObtenerTicketAsync(config, ct);

            var numero = await _wsfe.UltimoComprobanteAsync(
                token, sign, config.CuitEmisor, request.PuntoDeVenta, request.CodigoAfip, config.UsarHomologacion, ct) + 1;

            var wsfeReq = new WsfeCaeRequest
            {
                TipoComprobante = request.CodigoAfip,
                PuntoDeVenta = request.PuntoDeVenta,
                Numero = numero,
                Concepto = 2, // Servicios
                DocTipo = 80, // CUIT
                DocNro = long.Parse(request.CuitReceptor),
                FechaComprobante = request.FechaEmision,
                ImporteTotal = request.ImporteTotal,
                ServicioDesde = request.PeriodoServicioDesde,
                ServicioHasta = request.PeriodoServicioHasta,
                VencimientoPago = request.FechaVencimientoPago,
                ComprobanteAsociado = request.ComprobanteAsociado is { } a
                    ? new ComprobanteAsociadoWsfe { Tipo = a.Tipo, PuntoDeVenta = a.PuntoDeVenta, Numero = a.Numero, Cuit = long.Parse(a.CuitEmisor) }
                    : null
            };

            var resp = await _wsfe.SolicitarCaeAsync(token, sign, config.CuitEmisor, wsfeReq, config.UsarHomologacion, ct);
            if (!resp.Aprobado || string.IsNullOrWhiteSpace(resp.Cae))
            {
                _logger.LogError("AFIP rechazó el comprobante PV={PuntoVenta} Tipo={Tipo}: {Obs}",
                    request.PuntoDeVenta, request.CodigoAfip, resp.Observaciones);
                return ServiceResult<CaeResult>.Fail($"AFIP rechazó el comprobante: {resp.Observaciones}");
            }

            return ServiceResult<CaeResult>.Ok(new CaeResult
            {
                NumeroComprobante = resp.Numero == 0 ? numero : resp.Numero,
                Cae = resp.Cae,
                FechaVencimientoCae = resp.FechaVencimientoCae ?? request.FechaEmision.AddDays(10)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al solicitar CAE PV={PuntoVenta} Tipo={Tipo}", request.PuntoDeVenta, request.CodigoAfip);
            return ServiceResult<CaeResult>.Fail($"Error de comunicación con AFIP: {ex.Message}");
        }
    }

    public async Task<ServiceResult<bool>> VerificarServicioAsync(CancellationToken ct = default)
    {
        try
        {
            var config = await _configProvider.GetAsync(ct);
            var ok = await _wsfe.DummyAsync(config.UsarHomologacion, ct);
            return ok ? ServiceResult<bool>.Ok(true) : ServiceResult<bool>.Fail("WSFE no respondió OK.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Healthcheck AFIP falló");
            return ServiceResult<bool>.Fail($"No se pudo contactar AFIP: {ex.Message}");
        }
    }

    private Task<(string Token, string Sign)> ObtenerTicketAsync(AfipConfig config, CancellationToken ct)
        => _tokenCache.GetValidTicketAsync(async () =>
        {
            var traXml = TraBuilder.GenerarTraXml("wsfe");
            var cms = TraBuilder.FirmarCms(traXml, config.CertificadoRuta!, config.CertificadoPassword);
            var respuestaXml = await _wsaa.LoginCmsAsync(cms, config.UsarHomologacion, ct);
            return ParsearTicket(respuestaXml);
        }, ct);

    private static (string Token, string Sign, DateTime Expiration) ParsearTicket(string loginTicketResponseXml)
    {
        var doc = XDocument.Parse(loginTicketResponseXml);
        var token = doc.Descendants("token").First().Value;
        var sign = doc.Descendants("sign").First().Value;
        var expiration = DateTime.Parse(doc.Descendants("expirationTime").First().Value);
        return (token, sign, expiration);
    }
}
