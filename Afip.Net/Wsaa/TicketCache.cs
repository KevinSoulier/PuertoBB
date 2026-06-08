using System.Collections.Concurrent;

namespace Afip.Wsaa;

/// <summary>
/// Cache thread-safe del Ticket de Acceso WSAA, indexado por (CUIT, servicio).
/// AFIP emite un TA por servicio (wsfe, wsremcarne, ...) válido ~12 hs; se renueva con 10 min de margen.
/// La persistencia real (memoria o disco cifrado) la define el <see cref="ITicketStore"/> inyectado.
/// </summary>
public sealed class TicketCache
{
    private readonly ITicketStore _store;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();

    public TicketCache(ITicketStore store) => _store = store;

    public async Task<AfipTicket> GetOrRenewAsync(
        string cuit, string servicio, Func<Task<AfipTicket>> renovar, CancellationToken ct = default)
    {
        var clave = $"{cuit}:{servicio}";

        var actual = _store.Load(clave);
        if (Vigente(actual)) return actual!;

        var gate = _gates.GetOrAdd(clave, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            actual = _store.Load(clave);          // double-check tras tomar el lock
            if (Vigente(actual)) return actual!;

            var nuevo = await renovar();
            _store.Save(clave, nuevo);
            return nuevo;
        }
        finally
        {
            gate.Release();
        }
    }

    private static bool Vigente(AfipTicket? t) => t is not null && t.Expiration > DateTime.Now.AddMinutes(10);
}
