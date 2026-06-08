namespace Afip.Wsaa;

/// <summary>
/// Provee un Ticket de Acceso válido para un servicio de AFIP (ej. "wsfe", futuro "wsremcarne"),
/// resolviendo internamente la firma del TRA, el login en WSAA y el cacheo del TA.
/// Es el punto de reuso para sumar nuevos web services: todos comparten esta autenticación.
/// </summary>
public interface ITicketProvider
{
    Task<AfipTicket> GetTicketAsync(string servicio, AfipOptions options, CancellationToken ct = default);
}
