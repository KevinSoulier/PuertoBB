namespace PuertoBB.Core.Enums;

/// <summary>
/// Estados persistidos de un recibo.
/// "Vencido" NO es un estado: se calcula en presentación a partir de
/// FechaVencimientoPago (ver <see cref="Common.EstadoReciboHelper"/>).
/// </summary>
public enum ReciboEstado
{
    Emitido,
    Enviado,
    Pagado,
    Anulado
}
