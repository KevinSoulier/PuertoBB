using Afip.Wsaa;

namespace Afip.Mock;

/// <summary>
/// Proveedor de tickets falso: devuelve un AfipTicket hardcodeado sin requerir certificado ni red.
/// Registrarlo reemplaza a TicketProvider y permite usar WsfeService sin ninguna configuración AFIP.
/// </summary>
public sealed class MockTicketProvider : ITicketProvider
{
    public Task<AfipTicket> GetTicketAsync(string servicio, AfipOptions options, CancellationToken ct = default)
        => Task.FromResult(new AfipTicket("MOCK-TOKEN", "MOCK-SIGN", DateTime.UtcNow.AddHours(12)));
}
