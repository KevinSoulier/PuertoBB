namespace Afip.Wsfe;

/// <summary>
/// La solicitud de CAE (FECAESolicitar) se envió pero su respuesta se perdió (timeout/comunicación)
/// y la reconciliación posterior NO pudo confirmar el comprobante. El comprobante PODRÍA haberse
/// autorizado en AFIP: el caller debe advertir y verificar el último comprobante antes de reintentar
/// (un reintento ciego puede duplicar).
/// </summary>
public sealed class AfipRespuestaPerdidaException : Exception
{
    public AfipRespuestaPerdidaException(string message, Exception inner) : base(message, inner) { }
}
