namespace Afip.Documentos;

public record ItemDocumento
{
    public required string Descripcion { get; init; }
    public decimal Cantidad { get; init; } = 1;
    public decimal PrecioUnitario { get; init; }
    public decimal Subtotal { get; init; }
    public decimal? AlicuotaIva { get; init; }   // null = exento / no aplica (comprobante C)
}
