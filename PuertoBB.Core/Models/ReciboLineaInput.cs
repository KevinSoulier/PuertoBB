namespace PuertoBB.Core.Models;

/// <summary>
/// Línea de detalle ingresada al emitir un recibo individual (multi-ítem).
/// El total del recibo es la suma de los <see cref="Importe"/> de sus líneas.
/// </summary>
public record ReciboLineaInput(string Descripcion, decimal Cantidad, decimal PrecioUnitario)
{
    public decimal Importe => Cantidad * PrecioUnitario;
}
