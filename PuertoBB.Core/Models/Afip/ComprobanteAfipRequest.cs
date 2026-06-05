using PuertoBB.Core.Enums;

namespace PuertoBB.Core.Models.Afip;

/// <summary>
/// Datos neutros (independientes de la app) que necesita el servicio AFIP para solicitar un CAE.
/// El servicio AFIP obtiene el número (FECompUltimoAutorizado+1), arma el FEDetRequest y devuelve el CAE.
/// </summary>
public record ComprobanteAfipRequest
{
    public required TipoComprobante TipoComprobante { get; init; }
    public required int             CodigoAfip      { get; init; }   // 11 = Recibo C, 13 = NC C
    public required int             PuntoDeVenta    { get; init; }

    public required string   CuitReceptor { get; init; }            // sin guiones
    public required decimal  ImporteTotal { get; init; }
    public required DateTime FechaEmision { get; init; }

    // Concepto 2 = Servicios → requiere período de servicio + vto de pago
    public required int      PeriodoServicioDesde { get; init; }    // yyyyMMdd derivado del período
    public required int      PeriodoServicioHasta { get; init; }
    public required DateTime FechaVencimientoPago { get; init; }

    /// <summary>Comprobante asociado (solo Notas de Crédito): tipo/pv/número del recibo original.</summary>
    public ComprobanteAsociado? ComprobanteAsociado { get; init; }
}

public record ComprobanteAsociado
{
    public required int  Tipo         { get; init; }
    public required int  PuntoDeVenta { get; init; }
    public required long Numero       { get; init; }
    public required string CuitEmisor { get; init; }
}
