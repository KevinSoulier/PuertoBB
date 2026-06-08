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
    Anulado,

    /// <summary>
    /// Recibo creado pero sin CAE todavía (la solicitud a AFIP falló o no se intentó).
    /// Se persiste antes de pedir el CAE para que la emisión sea idempotente y reintentable.
    /// Va al final del enum para no renumerar los valores ya guardados.
    /// </summary>
    Pendiente
}
