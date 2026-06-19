namespace PuertoBB.Core.Enums;

/// <summary>
/// Eje de cobro del recibo. NO se persiste: se deriva de FechaIncobrable/FechaPago.
/// Incobrable y Pagado son mutuamente excluyentes (guarda en el service).
/// </summary>
public enum EstadoCobro
{
    PendienteDeCobro,
    Pagado,
    Incobrable
}
