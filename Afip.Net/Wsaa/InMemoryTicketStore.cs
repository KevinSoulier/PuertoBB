using System.Collections.Concurrent;

namespace Afip.Wsaa;

/// <summary>Almacén de tickets en memoria (no sobrevive reinicios). Es el default.</summary>
public sealed class InMemoryTicketStore : ITicketStore
{
    private readonly ConcurrentDictionary<string, AfipTicket> _items = new();

    public AfipTicket? Load(string clave) => _items.TryGetValue(clave, out var t) ? t : null;

    public void Save(string clave, AfipTicket ticket) => _items[clave] = ticket;
}
