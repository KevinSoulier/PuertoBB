using PuertoBB.Core.Enums;

namespace PuertoBB.Core.Common;

/// <summary>
/// Única fuente de presentación y acciones del recibo. El único estado persistido es el eje fiscal
/// (<see cref="EstadoFiscal"/>); envío, cobro y "vencido" se derivan de columnas que ya existen
/// (FechaEnvioMail/UltimoErrorMail, FechaPago/FechaIncobrable, FechaVencimientoPago).
/// </summary>
public static class EstadoReciboHelper
{
    /// <summary>Eje de envío del mail, derivado.</summary>
    public static EstadoEnvio Envio(IReciboEstadoView r)
        => r.FechaEnvioMail is not null ? EstadoEnvio.Enviado
         : r.UltimoErrorMail is not null ? EstadoEnvio.Fallido
         : EstadoEnvio.NoEnviado;

    /// <summary>Eje de cobro, derivado. Incobrable tiene prioridad sobre Pagado (son excluyentes).</summary>
    public static EstadoCobro Cobro(IReciboEstadoView r)
        => r.FechaIncobrable is not null ? EstadoCobro.Incobrable
         : r.FechaPago is not null ? EstadoCobro.Pagado
         : EstadoCobro.PendienteDeCobro;

    /// <summary>True si está visualmente vencido: emitido, aún por cobrar y pasado el vencimiento.</summary>
    public static bool EstaVencido(IReciboEstadoView r, DateTime hoy)
        => r.EstadoFiscal == EstadoFiscal.Emitido
           && Cobro(r) == EstadoCobro.PendienteDeCobro
           && r.FechaVencimientoPago.Date < hoy.Date;

    /// <summary>Días de atraso (0 si no está vencido).</summary>
    public static int DiasAtraso(IReciboEstadoView r, DateTime hoy)
        => EstaVencido(r, hoy) ? (hoy.Date - r.FechaVencimientoPago.Date).Days : 0;

    /// <summary>
    /// Columna "Estado" (fiscal + cobro) por prioridad:
    /// Anulado → Incobrable → Pagado → Pendiente → Vencido → Emitido.
    /// </summary>
    public static string EtiquetaEstado(IReciboEstadoView r, DateTime hoy)
        => r.EstadoFiscal switch
        {
            EstadoFiscal.Anulado   => "Anulado",
            EstadoFiscal.Pendiente => "Pendiente",
            _ => Cobro(r) switch
            {
                EstadoCobro.Incobrable => "Incobrable",
                EstadoCobro.Pagado     => "Pagado",
                _                      => EstaVencido(r, hoy) ? "Vencido" : "Emitido"
            }
        };

    /// <summary>Columna "Envío" (solo mail). Sin CAE (Pendiente) ⇒ "—"; un recibo anulado muestra
    /// el envío de su nota de crédito (Enviado/Mail falló/Sin enviar).</summary>
    public static string EtiquetaEnvio(IReciboEstadoView r)
        => r.EstadoFiscal is EstadoFiscal.Pendiente
            ? "—"
            : Envio(r) switch
            {
                EstadoEnvio.Enviado => "Enviado",
                EstadoEnvio.Fallido => "Mail falló",
                _                   => "Sin enviar"
            };

    /// <summary>
    /// Un recibo está "completo" (no hay nada que reintentar en el loop de emisión) cuando ya está
    /// Anulado, o quedó Emitido con el mail enviado. Compartido por los servicios de CP y CM.
    /// </summary>
    public static bool EsCompleto(IReciboEstadoView r)
        => r.EstadoFiscal == EstadoFiscal.Anulado
           || (r.EstadoFiscal == EstadoFiscal.Emitido && r.FechaEnvioMail is not null);
}

/// <summary>Acciones habilitadas para un recibo, derivadas en un solo lugar.</summary>
public readonly record struct AccionesRecibo(
    bool EsReintentable,
    bool EsEnviable,
    bool EsPagable,
    bool EsMarcableIncobrable,
    bool EsQuitableIncobrable,
    bool EsAnulable,
    bool EsPrevisualizable,
    bool TieneNotaCredito)
{
    public static AccionesRecibo De(IReciboEstadoView r)
    {
        var emitido = r.EstadoFiscal == EstadoFiscal.Emitido;
        var porCobrar = EstadoReciboHelper.Cobro(r) == EstadoCobro.PendienteDeCobro;
        return new AccionesRecibo(
            EsReintentable:       r.EstadoFiscal == EstadoFiscal.Pendiente,
            EsEnviable:           emitido,
            EsPagable:            emitido && porCobrar,
            EsMarcableIncobrable: emitido && porCobrar,
            EsQuitableIncobrable: EstadoReciboHelper.Cobro(r) == EstadoCobro.Incobrable,
            EsAnulable:           emitido,
            EsPrevisualizable:    r.EstadoFiscal != EstadoFiscal.Pendiente,
            TieneNotaCredito:     r.TieneNotaCredito);
    }
}
