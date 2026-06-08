namespace Afip.Documentos;

public record ComprobanteDocumento
{
    public required int CodigoTipo { get; init; }       // código AFIP: 1, 6, 11, 3, 8, 13, ...
    public required int PuntoVenta { get; init; }
    public required long Numero { get; init; }
    public required DateTime FechaEmision { get; init; }
    public string? LetraOverride { get; init; }         // null = derivar del catálogo
    public string? NombreOverride { get; init; }        // null = derivar del catálogo

    public required string Cae { get; init; }
    public required DateTime FechaVencimientoCae { get; init; }

    public required decimal ImporteTotal { get; init; }
    public decimal? ImporteNeto { get; init; }
    public decimal? ImporteIva { get; init; }
    public decimal? ImporteExento { get; init; }

    public DateOnly? PeriodoServicioDesde { get; init; }
    public DateOnly? PeriodoServicioHasta { get; init; }
    public DateTime? FechaVencimientoPago { get; init; }

    public ComprobanteAsociado? ComprobanteAsociado { get; init; }

    public string? CondicionVenta { get; init; }              // ej. "Transferencia Bancaria / Contado"
    public string? ConceptoGeneral { get; init; }           // texto libre (recibo sin items)
    public IReadOnlyList<ItemDocumento> Items { get; init; } = [];
    public IReadOnlyList<string> Leyendas { get; init; } = [];

    public required EmisorDocumento Emisor { get; init; }
    public required ReceptorDocumento Receptor { get; init; }
}
