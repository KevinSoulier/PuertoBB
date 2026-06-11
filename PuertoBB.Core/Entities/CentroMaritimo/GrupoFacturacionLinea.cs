using PuertoBB.Core.Entities.Common;

namespace PuertoBB.Core.Entities.CentroMaritimo;

/// <summary>
/// Ítem de detalle de un <see cref="GrupoFacturacion"/>. Define los conceptos a facturar a cada
/// miembro del grupo: al emitir, cada recibo materializa una <see cref="ReciboLinea"/> por ítem.
/// El <see cref="GrupoFacturacion.Importe"/> es la suma de los <see cref="Importe"/> de sus líneas.
/// </summary>
public class GrupoFacturacionLinea : BaseEntity
{
    public int              GrupoFacturacionId { get; set; }
    public GrupoFacturacion Grupo              { get; set; } = null!;

    /// <summary>Descripción del ítem (concepto facturado).</summary>
    public string  Descripcion    { get; set; } = string.Empty;

    /// <summary>Cantidad de unidades.</summary>
    public decimal Cantidad       { get; set; } = 1;

    /// <summary>Precio por unidad.</summary>
    public decimal PrecioUnitario { get; set; }

    /// <summary>Subtotal del ítem (= Cantidad × PrecioUnitario).</summary>
    public decimal Importe        { get; set; }

    /// <summary>Orden de aparición en el comprobante.</summary>
    public int     Orden          { get; set; }
}
