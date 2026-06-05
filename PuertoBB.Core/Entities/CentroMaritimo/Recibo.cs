using PuertoBB.Core.Entities.Common;
using PuertoBB.Core.Enums;

namespace PuertoBB.Core.Entities.CentroMaritimo;

/// <summary>
/// Recibo emitido a una Agencia. Índice único:
/// (AgenciaId, GrupoFacturacionId, PeriodoAnio, PeriodoMes).
/// Recibos consolidados únicos por (AgenciaId, PeriodoAnio, PeriodoMes) WHERE EsConsolidadoVouchers=true.
/// </summary>
public class Recibo : BaseEntity
{
    public int               AgenciaId          { get; set; }
    public Agencia           Agencia            { get; set; } = null!;
    public int?              GrupoFacturacionId { get; set; }
    public GrupoFacturacion? Grupo              { get; set; }

    public int     PeriodoAnio { get; set; }
    public int     PeriodoMes  { get; set; }
    public decimal Importe     { get; set; } // = SUM(Vouchers.Importe) cuando EsConsolidadoVouchers=true
    public string  Detalle     { get; set; } = string.Empty;

    public bool EsConsolidadoVouchers { get; set; }

    // Apoderado fiscal (copiado desde Configuracion al emitir, para inmutabilidad)
    public bool    EsApoderado     { get; set; }
    public string? NombreApoderado { get; set; }
    public string? CuitApoderado   { get; set; }

    // Comprobante AFIP
    public int             PuntoDeVenta        { get; set; }
    public TipoComprobante TipoComprobante     { get; set; }
    public int             CodigoAfip          { get; set; }
    public long            NumeroComprobante   { get; set; }
    public string          CAE                 { get; set; } = string.Empty;
    public DateTime        FechaVencimientoCAE { get; set; }
    public DateTime        FechaEmision        { get; set; }

    public ReciboEstado Estado { get; set; } = ReciboEstado.Emitido;

    // Control de pagos
    public DateTime  FechaVencimientoPago { get; set; }
    public DateTime? FechaPago            { get; set; }

    public ICollection<Voucher> Vouchers      { get; set; } = [];
    public NotaDeCredito?       NotaDeCredito { get; set; }
}
