namespace PuertoBB.Core.Models.Resultados;

/// <summary>
/// Vista de una agencia en la pantalla de Cierre de Período: sus vouchers del período + total +
/// estado, y los consolidados ya emitidos (0..n: el original más eventuales complementarios).
/// </summary>
public record ClienteCierrePeriodoVm
{
    public required int    ClienteId     { get; init; }
    public required string ClienteNombre { get; init; }
    public required IReadOnlyList<VoucherCierreVm> Vouchers { get; init; }
    public required decimal Total { get; init; }
    public required EstadoCierreCliente Estado { get; init; }

    /// <summary>Consolidados de la agencia en el período (original + complementarios), sin los anulados.</summary>
    public required IReadOnlyList<ConsolidadoCierreVm> Consolidados { get; init; }

    /// <summary>Vouchers todavía libres (sin consolidar): lo que generará la próxima emisión (1ª o complementario).</summary>
    public int VouchersLibres => Vouchers.Count(v => v.Libre);

    /// <summary>Total de los vouchers libres.</summary>
    public decimal TotalLibre => Vouchers.Where(v => v.Libre).Sum(v => v.Importe);

    /// <summary>True si ya hay al menos un consolidado: la próxima emisión sería un complementario.</summary>
    public bool TieneConsolidados => Consolidados.Count > 0;
}

/// <summary>Un recibo consolidado de la agencia en el período (con su número y estado), para listar/operar por recibo.</summary>
public record ConsolidadoCierreVm(int ReciboId, long NumeroComprobante, decimal Importe, int CantVouchers, EstadoCierreCliente Estado);

public record VoucherCierreVm(int Id, int Numero, string Barco, DateTime Fecha, decimal Importe, bool Libre, long? NumeroComprobante)
{
    /// <summary>Texto para la columna "Comprobante" de la sublista: Libre / Pendiente / N° de comprobante.</summary>
    public string ComprobanteTexto => Libre ? "Libre" : NumeroComprobante is > 0 ? $"N° {NumeroComprobante}" : "Pendiente";
}

public enum EstadoCierreCliente
{
    /// <summary>Hay vouchers libres (o un consolidado sin CAE) por emitir.</summary>
    Pendiente,
    /// <summary>Todos los vouchers están consolidados con CAE; algún recibo aún sin enviar por mail.</summary>
    Emitido,
    /// <summary>Todos los consolidados están enviados (o pagados) y no quedan vouchers libres.</summary>
    Completo
}
