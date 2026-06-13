using System.ServiceModel;
using Afip.Abstractions;
using Afip.Soap.Padron;

namespace Afip.Soap;

/// <summary>
/// Implementación real de <see cref="IPadronClient"/> sobre el cliente SOAP generado
/// (Consulta a Padrón — Constancia de Inscripción, personaServiceA5).
/// </summary>
public class PadronSoapClient : IPadronClient
{
    private const string UrlHomologacion = "https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA5";
    private const string UrlProduccion   = "https://aws.afip.gov.ar/sr-padron/webservices/personaServiceA5";

    private static PersonaServiceA5Client Crear(bool homologacion)
    {
        var url = homologacion ? UrlHomologacion : UrlProduccion;
        return new PersonaServiceA5Client(PersonaServiceA5Client.EndpointConfiguration.PersonaServiceA5Port, new EndpointAddress(url));
    }

    public async Task<PadronPersona?> ConsultarAsync(string token, string sign, string cuitRepresentada, long idPersona,
        bool usarHomologacion, CancellationToken ct = default)
    {
        var client = Crear(usarHomologacion);
        try
        {
            var r = await client.getPersona_v2Async(token, sign, long.Parse(cuitRepresentada), idPersona);
            await client.CloseAsync();
            return PadronMapper.ToPersona(r.personaReturn);
        }
        catch (FaultException ex) when (EsPersonaInexistente(ex))
        {
            client.Abort();
            return null;
        }
        catch { client.Abort(); throw; }
    }

    /// <summary>Único punto de detección del fault "persona inexistente" (frágil por diseño de ARCA:
    /// se identifica por el texto del mensaje).</summary>
    private static bool EsPersonaInexistente(FaultException ex)
        => ex.Message.Contains("No existe", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("no figura", StringComparison.OrdinalIgnoreCase);
}
