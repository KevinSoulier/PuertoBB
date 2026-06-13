using System.Security.Cryptography;
using System.Text;
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
        // El certificado puede venir como contenido en memoria (caso normal: se carga desde la base) o
        // por ruta de archivo. Alinear con TraBuilder, que prioriza el contenido en memoria.
        if (string.IsNullOrWhiteSpace(options.CertificadoRuta) && options.CertificadoContenido is not { Length: > 0 })
            throw new InvalidOperationException("No hay certificado configurado para autenticar contra WSAA.");

        return _cache.GetOrRenewAsync(options.Cuit, servicio, async () =>
        {
            var traXml = TraBuilder.GenerarTraXml(servicio);
            var cms = TraBuilder.FirmarCms(traXml, options);
            var respuestaXml = await _wsaa.LoginCmsAsync(cms, options.UsarHomologacion, ct);
            return ParsearTicket(respuestaXml);
        }, ct, HuellaCredenciales(options));
    }

    /// <summary>
    /// Huella estable de las credenciales + ambiente. Si cambia el certificado, la clave, la contraseña
    /// o el ambiente, cambia la huella y el TA cacheado deja de reutilizarse (se reautentica de verdad).
    /// </summary>
    private static string HuellaCredenciales(AfipOptions o)
    {
        using var ms = new MemoryStream();
        if (o.CertificadoContenido is { Length: > 0 } cert) ms.Write(cert);
        if (o.CertificadoKeyContenido is { Length: > 0 } key) ms.Write(key);
        var meta = $"{o.CertificadoPassword}|{o.CertificadoRuta}|{o.CertificadoKeyRuta}|{o.UsarHomologacion}";
        ms.Write(Encoding.UTF8.GetBytes(meta));
        return Convert.ToHexString(SHA256.HashData(ms.ToArray()))[..16];
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
