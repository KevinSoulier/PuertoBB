using PuertoBB.Core.Entities.Common;

namespace PuertoBB.Core.Entities.CentroMaritimo;

/// <summary>
/// Línea/ítem del detalle de un <see cref="Recibo"/>. Se persiste como snapshot inmutable al emitir:
/// el detalle mostrado/enviado (UI, PDF, mail) sale SIEMPRE de estas líneas, no se recalcula.
/// Para recibos consolidados se materializa una línea por voucher (en vez de derivarlo en el PDF).
/// El <see cref="Recibo.Importe"/> es la suma de los <see cref="Importe"/> de sus líneas.
/// </summary>
public class ReciboLinea : BaseEntity
{
    public int     ReciboId    { get; set; }
    public Recibo  Recibo      { get; set; } = null!;

    /// <summary>Descripción del ítem (concepto facturado o voucher consolidado).</summary>
    public string  Descripcion    { get; set; } = string.Empty;

    /// <summary>Cantidad de unidades.</summary>
    public decimal Cantidad       { get; set; } = 1;

    /// <summary>Precio por unidad.</summary>
    public decimal PrecioUnitario { get; set; }

    /// <summary>Subtotal del ítem (= Cantidad × PrecioUnitario, guardado como snapshot).</summary>
    public decimal Importe        { get; set; }

    /// <summary>Orden de aparición en el comprobante.</summary>
    public int     Orden          { get; set; }
}
