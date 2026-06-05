using PuertoBB.Core.Entities.Common;
using PuertoBB.Core.Enums;

namespace PuertoBB.Core.Entities.CamaraPortuaria;

/// <summary>
/// Recibo emitido a una Empresa. Índice único:
/// (EmpresaId, GrupoFacturacionId, PeriodoAnio, PeriodoMes).
/// </summary>
public class Recibo : BaseEntity
{
    public int              EmpresaId          { get; set; }
    public Empresa          Empresa            { get; set; } = null!;
    public int?             GrupoFacturacionId { get; set; } // null = emisión individual
    public GrupoFacturacion? Grupo             { get; set; }

    public int     PeriodoAnio { get; set; }
    public int     PeriodoMes  { get; set; }
    public decimal Importe     { get; set; }
    public string  Detalle     { get; set; } = string.Empty;

    // Comprobante AFIP
    public int             PuntoDeVenta        { get; set; }
    public TipoComprobante TipoComprobante     { get; set; }
    public int             CodigoAfip          { get; set; } // código numérico AFIP (ej. 11)
    public long            NumeroComprobante   { get; set; }
    public string          CAE                 { get; set; } = string.Empty;
    public DateTime        FechaVencimientoCAE { get; set; }
    public DateTime        FechaEmision        { get; set; }

    public ReciboEstado Estado { get; set; } = ReciboEstado.Emitido;

    // Control de pagos
    public DateTime  FechaVencimientoPago { get; set; } // = FechaEmision + Configuracion.DiasVencimiento
    public DateTime? FechaPago            { get; set; } // null hasta marcar como pagado

    public NotaDeCredito? NotaDeCredito { get; set; }
}
