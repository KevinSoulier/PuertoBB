using Afip.Abstractions;

namespace Afip.Mock;

/// <summary>
/// Cliente WSAA falso: devuelve un XML de ticket sin hacer llamadas de red ni requerir certificado.
/// El XML tiene el formato que espera <c>TicketProvider.ParsearTicket()</c> (elementos token, sign,
/// expirationTime). Útil cuando se quiere probar el flujo completo de TraBuilder+TicketCache pero
/// sin contactar wsaa.afip.gov.ar. Si no se necesita certificado tampoco, usar MockTicketProvider.
/// </summary>
public sealed class MockWsaaClient : IWsaaClient
{
    public Task<string> LoginCmsAsync(string cmsFirmadoBase64, bool usarHomologacion, CancellationToken ct = default)
    {
        var expiration = DateTime.UtcNow.AddHours(12).ToString("o");
        var xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <loginTicketResponse version="1.0">
              <header>
                <source>MOCK</source>
                <expirationTime>{expiration}</expirationTime>
              </header>
              <credentials>
                <token>MOCK-TOKEN</token>
                <sign>MOCK-SIGN</sign>
              </credentials>
            </loginTicketResponse>
            """;
        return Task.FromResult(xml);
    }
}
