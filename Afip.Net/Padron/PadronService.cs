using Afip.Abstractions;
using Afip.Wsaa;

namespace Afip.Padron;

/// <summary>
/// Implementación de <see cref="IPadronService"/>: obtiene el TA del servicio
/// <c>ws_sr_constancia_inscripcion</c> vía <see cref="ITicketProvider"/> (cache por (CUIT, servicio),
/// independiente del TA de wsfe) y consulta getPersona_v2.
/// </summary>
public sealed class PadronService : IPadronService
{
    public const string Servicio = "ws_sr_constancia_inscripcion";

    private readonly ITicketProvider _ticket;
    private readonly IPadronClient _padron;

    public PadronService(ITicketProvider ticket, IPadronClient padron)
    {
        _ticket = ticket;
        _padron = padron;
    }

    public async Task<PadronPersona?> ConsultarPersonaAsync(AfipOptions options, long cuit, CancellationToken ct = default)
    {
        var t = await _ticket.GetTicketAsync(Servicio, options, ct);
        return await _padron.ConsultarAsync(t.Token, t.Sign, options.Cuit, cuit, options.UsarHomologacion, ct);
    }
}
