namespace Afip.Wsfe;

/// <summary>
/// Datos de negocio para solicitar un CAE vía WSFE. El número de comprobante se resuelve
/// internamente (FECompUltimoAutorizado + 1), por lo que no se incluye acá.
/// Pensado para comprobantes de servicios (Concepto = 2): requiere período de servicio + vencimiento de pago.
/// </summary>
public sealed record AfipComprobanteRequest
{
    /// <summary>Código de comprobante AFIP (11 = Recibo C, 13 = Nota de Crédito C, etc.).</summary>
    public required int      CodigoComprobante { get; init; }
    public required int      PuntoDeVenta      { get; init; }
    public required long     DocNroReceptor    { get; init; }
    public required decimal  ImporteTotal      { get; init; }
    public required DateTime FechaComprobante  { get; init; }

    /// <summary>Período de servicio (yyyyMMdd) y vencimiento de pago (requeridos para Concepto = 2).</summary>
    public required int      ServicioDesde   { get; init; }
    public required int      ServicioHasta   { get; init; }
    public required DateTime VencimientoPago { get; init; }

    /// <summary>1 = Productos, 2 = Servicios (default), 3 = Productos y Servicios.</summary>
    public int Concepto { get; init; } = 2;

    /// <summary>Tipo de documento del receptor. 80 = CUIT (default).</summary>
    public int DocTipoReceptor { get; init; } = 80;

    /// <summary>Comprobante asociado (solo Notas de Crédito).</summary>
    public AfipComprobanteAsociado? ComprobanteAsociado { get; init; }
}
