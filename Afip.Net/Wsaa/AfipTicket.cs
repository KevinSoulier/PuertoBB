namespace Afip.Wsaa;

/// <summary>
/// Ticket de Acceso (TA) emitido por WSAA: token + firma, válido ~12 hs para un servicio puntual.
/// </summary>
public sealed record AfipTicket(string Token, string Sign, DateTime Expiration);
