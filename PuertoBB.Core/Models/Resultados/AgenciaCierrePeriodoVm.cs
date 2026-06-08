namespace PuertoBB.Core.Models.Resultados;

/// <summary>
/// Vista de una agencia en la pantalla de Cierre de Período:
/// sus vouchers del período + total + estado del recibo consolidado (si existe).
/// </summary>
public record AgenciaCierrePeriodoVm
{
    public required int    AgenciaId     { get; init; }
    public required string AgenciaNombre { get; init; }
    public required IReadOnlyList<VoucherCierreVm> Vouchers { get; init; }
    public required decimal Total { get; init; }
    public required EstadoCierreAgencia Estado { get; init; }
    public long? NumeroComprobante { get; init; }
    public int?  ReciboId          { get; init; }
}

public record VoucherCierreVm(int Id, int Numero, string Barco, DateTime Fecha, decimal Importe);

public enum EstadoCierreAgencia
{
    /// <summary>No hay recibo consolidado para esta agencia en el período.</summary>
    Pendiente,
    /// <summary>Recibo consolidado emitido (CAE obtenido) pero mail aún no enviado.</summary>
    Emitido,
    /// <summary>Recibo consolidado emitido y mail enviado (o pagado).</summary>
    Completo
}
