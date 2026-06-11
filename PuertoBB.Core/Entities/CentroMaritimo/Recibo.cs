using PuertoBB.Core.Entities.Common;
using PuertoBB.Core.Enums;

namespace PuertoBB.Core.Entities.CentroMaritimo;

/// <summary>
/// Recibo emitido a una Agencia. Entidad de auditoría autocontenida: los datos fiscales del
/// receptor se copian al emitir (Receptor*) y el vínculo con el grupo que lo originó vive en
/// <see cref="EmisionGrupo"/> (null = emisión individual/consolidado).
/// Recibos consolidados únicos por (AgenciaId, PeriodoAnio, PeriodoMes) WHERE EsConsolidadoVouchers=true;
/// el anti-duplicados de emisión por grupo vive en el índice único de EmisionesGrupo.
/// </summary>
public class Recibo : BaseEntity
{
    public int     AgenciaId { get; set; }
    public Agencia Agencia   { get; set; } = null!;

    /// <summary>Vínculo con la emisión de grupo que lo originó; null = individual o consolidado.</summary>
    public EmisionGrupo? EmisionGrupo { get; set; }

    // Snapshot fiscal del receptor (copiado al emitir, inmutable — como los campos de apoderado)
    public string  ReceptorNombre       { get; set; } = string.Empty;
    public string  ReceptorRazonSocial  { get; set; } = string.Empty;
    public string  ReceptorCuit         { get; set; } = string.Empty;
    public string? ReceptorDomicilio    { get; set; }
    public string? ReceptorCondicionIva { get; set; }

    public int     PeriodoAnio { get; set; }
    public int     PeriodoMes  { get; set; }
    public decimal Importe     { get; set; } // = suma de Items.Importe (se persiste al emitir)
    public string  Detalle     { get; set; } = string.Empty; // encabezado/leyenda opcional; el detalle real son los Items

    /// <summary>Líneas/ítems del recibo (snapshot inmutable del detalle). El detalle mostrado/enviado sale de acá.</summary>
    public ICollection<ReciboLinea> Lineas { get; set; } = [];

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

    // Trazabilidad de emisión (para mostrar estado y permitir reintento idempotente).
    public string?   UltimoErrorCae  { get; set; } // null = CAE OK; con texto = por qué quedó Pendiente
    public string?   UltimoErrorMail { get; set; } // null = el mail no falló; con texto = por qué no se envió
    public DateTime? FechaEnvioMail  { get; set; } // null = mail no enviado

    // Control de pagos
    public DateTime  FechaVencimientoPago { get; set; }
    public DateTime? FechaPago            { get; set; }

    public ICollection<Voucher> Vouchers      { get; set; } = [];
    public NotaDeCredito?       NotaDeCredito { get; set; }
}
