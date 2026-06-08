using System.Xml.Linq;
using Afip.Abstractions;

namespace Afip.Wsaa;

/// <summary>
/// Implementación de <see cref="ITicketProvider"/>: genera y firma el TRA, llama a WSAA (loginCms)
/// y cachea el TA por (CUIT, servicio) vía <see cref="TicketCache"/>.
/// </summary>
public sealed class TicketProvider : ITicketProvider
{
    private readonly IWsaaClient _wsaa;
    private readonly TicketCache _cache;

    public TicketProvider(IWsaaClient wsaa, TicketCache cache)
    {
        _wsaa = wsaa;
        _cache = cache;
    }

    public Task<AfipTicket> GetTicketAsync(string servicio, AfipOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.CertificadoRuta))
            throw new InvalidOperationException("No hay certificado configurado para autenticar contra WSAA.");

        return _cache.GetOrRenewAsync(options.Cuit, servicio, async () =>
        {
            var traXml = TraBuilder.GenerarTraXml(servicio);
            var cms = TraBuilder.FirmarCms(traXml, options);
            var respuestaXml = await _wsaa.LoginCmsAsync(cms, options.UsarHomologacion, ct);
            return ParsearTicket(respuestaXml);
        }, ct);
    }

    private static AfipTicket ParsearTicket(string loginTicketResponseXml)
    {
        var doc = XDocument.Parse(loginTicketResponseXml);
        var token = doc.Descendants("token").First().Value;
        var sign = doc.Descendants("sign").First().Value;
        var expiration = DateTime.Parse(doc.Descendants("expirationTime").First().Value);
        return new AfipTicket(token, sign, expiration);
    }
}
