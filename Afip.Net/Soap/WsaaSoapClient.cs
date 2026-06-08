using System.ServiceModel;
using Afip.Abstractions;
using Afip.Soap.Wsaa;

namespace Afip.Soap;

/// <summary>
/// Implementación real de <see cref="IWsaaClient"/> sobre el cliente SOAP generado (LoginCms).
/// </summary>
public class WsaaSoapClient : IWsaaClient
{
    private const string UrlHomologacion = "https://wsaahomo.afip.gov.ar/ws/services/LoginCms";
    private const string UrlProduccion   = "https://wsaa.afip.gov.ar/ws/services/LoginCms";

    public async Task<string> LoginCmsAsync(string cmsFirmadoBase64, bool usarHomologacion, CancellationToken ct = default)
    {
        var url = usarHomologacion ? UrlHomologacion : UrlProduccion;
        var client = new LoginCMSClient(LoginCMSClient.EndpointConfiguration.LoginCms, new EndpointAddress(url));
        try
        {
            var respuesta = await client.loginCmsAsync(cmsFirmadoBase64);
            await client.CloseAsync();
            return respuesta.loginCmsReturn;
        }
        catch
        {
            client.Abort();
            throw;
        }
    }
}
