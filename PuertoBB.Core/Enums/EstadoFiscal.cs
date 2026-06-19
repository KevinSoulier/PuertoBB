namespace PuertoBB.Core.Enums;

/// <summary>
/// Único estado de flujo persistido del recibo: su ciclo fiscal/AFIP.
/// Los otros ejes (envío de mail y cobro) NO se persisten como estado: se derivan de
/// FechaEnvioMail/UltimoErrorMail y FechaPago/FechaIncobrable (ver <see cref="Common.EstadoReciboHelper"/>).
/// </summary>
public enum EstadoFiscal
{
    /// <summary>Recibo creado sin CAE todavía (la solicitud a AFIP falló o no se intentó). Reintentable.</summary>
    Pendiente,

    /// <summary>CAE autorizado por AFIP.</summary>
    Emitido,

    /// <summary>Anulado con Nota de Crédito.</summary>
    Anulado
}
