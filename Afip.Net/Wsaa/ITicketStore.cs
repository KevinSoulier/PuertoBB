namespace Afip.Wsaa;

/// <summary>
/// Almacén del Ticket de Acceso. La clave incluye CUIT + servicio (ej. "20111111112:wsfe").
/// Implementaciones: <see cref="InMemoryTicketStore"/> (default) y <see cref="FileTicketStore"/> (JSON en disco).
/// </summary>
public interface ITicketStore
{
    AfipTicket? Load(string clave);
    void Save(string clave, AfipTicket ticket);
}
