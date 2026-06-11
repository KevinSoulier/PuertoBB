using PuertoBB.Services.Common;

namespace CentroMaritimo.UI.ViewModels.Items;

/// <summary>Línea de detalle agregada al emitir un recibo individual multi-ítem.</summary>
public class LineaEmisionItem(string descripcion, decimal cantidad, decimal precioUnitario)
{
    public string  Descripcion     { get; } = descripcion;
    public decimal Cantidad        { get; } = cantidad;
    public decimal PrecioUnitario  { get; } = precioUnitario;
    public decimal Importe         => Cantidad * PrecioUnitario;
    public string  CantidadTexto   => Cantidad.ToString("G");
    public string  PrecioUnitTexto => Formato.Moneda(PrecioUnitario);
    public string  ImporteTexto    => Formato.Moneda(Importe);
}
