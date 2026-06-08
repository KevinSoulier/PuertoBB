namespace Afip.Wsfe;

/// <summary>Comprobante asociado (ej. el recibo original al que refiere una Nota de Crédito).</summary>
public sealed record AfipComprobanteAsociado
{
    public required int  Tipo         { get; init; }
    public required int  PuntoDeVenta { get; init; }
    public required long Numero       { get; init; }
    public required long Cuit         { get; init; }
}
