using PuertoBB.Core.Entities.Common;
using PuertoBB.Core.Enums;

namespace PuertoBB.Core.Entities.CamaraPortuaria;

public class NotaDeCredito : BaseEntity
{
    public int    ReciboOriginalId { get; set; }
    public Recibo ReciboOriginal   { get; set; } = null!;

    public int             PuntoDeVenta        { get; set; }
    public TipoComprobante TipoComprobante     { get; set; }
    public int             CodigoAfip          { get; set; }
    public long            NumeroComprobante   { get; set; }
    public string          CAE                 { get; set; } = string.Empty;
    public DateTime        FechaVencimientoCAE { get; set; }
    public DateTime        FechaEmision        { get; set; }
}
