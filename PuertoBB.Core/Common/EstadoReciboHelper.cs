using PuertoBB.Core.Enums;

namespace PuertoBB.Core.Common;

/// <summary>
/// "Vencido" no es un estado persistido (ver doc/arquitectura/datos.md).
/// Se calcula en presentación a partir de FechaVencimientoPago.
/// </summary>
public static class EstadoReciboHelper
{
    /// <summary>True si el recibo está visualmente vencido a la fecha indicada.</summary>
    public static bool EstaVencido(ReciboEstado estado, DateTime fechaVencimientoPago, DateTime hoy)
        => fechaVencimientoPago.Date < hoy.Date
           && estado is ReciboEstado.Emitido or ReciboEstado.Enviado;

    /// <summary>Días de atraso (0 si no está vencido).</summary>
    public static int DiasAtraso(ReciboEstado estado, DateTime fechaVencimientoPago, DateTime hoy)
        => EstaVencido(estado, fechaVencimientoPago, hoy)
            ? (hoy.Date - fechaVencimientoPago.Date).Days
            : 0;

    /// <summary>Etiqueta de presentación: el estado persistido, salvo que esté visualmente vencido.</summary>
    public static string EtiquetaEstado(ReciboEstado estado, DateTime fechaVencimientoPago, DateTime hoy)
        => EstaVencido(estado, fechaVencimientoPago, hoy) ? "Vencido" : estado.ToString();
}
