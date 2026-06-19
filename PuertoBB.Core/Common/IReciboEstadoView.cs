using PuertoBB.Core.Enums;

namespace PuertoBB.Core.Common;

/// <summary>
/// Vista mínima de un recibo para derivar su estado en un solo lugar (<see cref="EstadoReciboHelper"/>).
/// La implementan las entidades Recibo de CP y CM, que ya tienen todos estos campos.
/// </summary>
public interface IReciboEstadoView
{
    EstadoFiscal EstadoFiscal { get; }
    string       CAE { get; }
    DateTime?    FechaEnvioMail { get; }
    string?      UltimoErrorMail { get; }
    string?      UltimoErrorCae { get; }
    DateTime?    FechaPago { get; }
    DateTime?    FechaIncobrable { get; }
    DateTime     FechaVencimientoPago { get; }
    bool         TieneNotaCredito { get; }
}
