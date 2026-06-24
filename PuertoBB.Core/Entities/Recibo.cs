using System.ComponentModel.DataAnnotations.Schema;
using PuertoBB.Core.Common;
using PuertoBB.Core.Entities.Common;
using PuertoBB.Core.Enums;

namespace PuertoBB.Core.Entities;

/// <summary>
/// Recibo emitido a una Cliente. Cliente de auditoría autocontenida: los datos fiscales del
/// receptor se copian al emitir (Receptor*) y el vínculo con el grupo que lo originó vive en
/// <see cref="EmisionGrupo"/> (null = emisión individual).
/// El anti-duplicados de emisión por grupo vive en el índice único de EmisionesGrupo.
/// </summary>
public class Recibo : BaseEntity, IReciboBusquedaView
{
    public int     ClienteId { get; set; }
    public Cliente Cliente   { get; set; } = null!;

    /// <summary>Vínculo con la emisión de grupo que lo originó; null = emisión individual.</summary>
    public EmisionGrupo? EmisionGrupo { get; set; }

    // Snapshot fiscal del receptor (copiado al emitir, inmutable)
    public string  ReceptorNombre       { get; set; } = string.Empty;
    public string  ReceptorRazonSocial  { get; set; } = string.Empty;
    public string  ReceptorCuit         { get; set; } = string.Empty;
    public string? ReceptorDomicilio    { get; set; }
    public string? ReceptorCondicionIva { get; set; }
    /// <summary>Código AFIP de la condición IVA del receptor al emitir (RG 5616); el texto de arriba se deriva del catálogo.</summary>
    public int?    ReceptorCondicionIvaId { get; set; }

    public int     PeriodoAnio { get; set; }
    public int     PeriodoMes  { get; set; }
    public decimal Importe     { get; set; } // = suma de Items.Importe (se persiste al emitir)
    public string  Detalle     { get; set; } = string.Empty; // encabezado/leyenda opcional; el detalle real son los Items

    /// <summary>Líneas/ítems del recibo (snapshot inmutable del detalle). El detalle mostrado/enviado sale de acá.</summary>
    public ICollection<ReciboLinea> Lineas { get; set; } = [];

    // Comprobante AFIP
    public int             PuntoDeVenta        { get; set; }
    public TipoComprobante TipoComprobante     { get; set; }
    public int             CodigoAfip          { get; set; } // código numérico AFIP (ej. 11)
    public long            NumeroComprobante   { get; set; }
    public string          CAE                 { get; set; } = string.Empty;
    public DateTime        FechaVencimientoCAE { get; set; }
    public DateTime        FechaEmision        { get; set; }

    /// <summary>Único estado de flujo persistido (eje fiscal). Envío y cobro se derivan.</summary>
    public EstadoFiscal EstadoFiscal { get; set; } = EstadoFiscal.Emitido;

    // Trazabilidad de emisión (para mostrar estado y permitir reintento idempotente).
    public string?   UltimoErrorCae  { get; set; } // null = CAE OK; con texto = por qué quedó Pendiente
    public string?   UltimoErrorMail { get; set; } // null = el mail no falló; con texto = por qué no se envió
    public DateTime? FechaEnvioMail  { get; set; } // null = mail no enviado

    // Control de pagos (eje de cobro: Pendiente de cobro / Pagado / Incobrable, derivado de estas fechas)
    public DateTime  FechaVencimientoPago { get; set; } // = FechaEmision + Configuracion.DiasVencimiento
    public DateTime? FechaPago            { get; set; } // null hasta marcar como pagado
    public DateTime? FechaIncobrable      { get; set; } // null = no dado de baja; excluyente con FechaPago
    public string?   MotivoIncobrable     { get; set; } // motivo opcional de la baja (auditoría)

    public NotaDeCredito? NotaDeCredito { get; set; }

    /// <summary>True si tiene Nota de Crédito asociada (recibo anulado). Derivado.</summary>
    [NotMapped]
    public bool TieneNotaCredito => NotaDeCredito is not null;
}
