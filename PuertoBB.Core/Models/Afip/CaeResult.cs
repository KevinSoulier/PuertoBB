namespace PuertoBB.Core.Models.Afip;

/// <summary>Resultado de una solicitud de CAE a WSFE.</summary>
public record CaeResult
{
    public required long     NumeroComprobante   { get; init; }
    public required string   Cae                 { get; init; }
    public required DateTime FechaVencimientoCae { get; init; }
}
