using System.ComponentModel.DataAnnotations.Schema;
using PuertoBB.Core.Entities.Common;

namespace PuertoBB.Core.Entities.CentroMaritimo;

/// <summary>Configuración singleton (Id = 1) del Centro Marítimo.</summary>
public class Configuracion : BaseEntity
{
    public string    RazonSocial       { get; set; } = string.Empty;
    public string    Cuit              { get; set; } = string.Empty;
    public string?   IngresosBrutos    { get; set; }
    public DateTime? InicioActividades { get; set; }

    // Tipos AFIP (configurables; default = clase C / Exento IVA).
    // La Nota de Crédito se deriva de la clase del comprobante (ver CatalogoComprobantesAfip).
    public int CodigoAfipRecibo        { get; set; } = 15; // Recibo C
    public int CodigoAfipNotaDeCredito { get; set; } = 13; // Nota de Crédito C

    // Puntos de venta AFIP (cada uno con su ambiente y certificado). Uno queda como activo.
    public List<PuntoDeVenta> PuntosDeVenta { get; set; } = new();

    /// <summary>Punto de venta activo (el que la app usa para emitir). Null si no hay ninguno marcado.</summary>
    [NotMapped]
    public PuntoDeVenta? PuntoDeVentaActivo => PuntosDeVenta.FirstOrDefault(p => p.Activo);

    // Vouchers
    public decimal ImporteVoucherPredeterminado { get; set; } = 0;

    // Control de pagos
    public int DiasVencimiento { get; set; } = 15;

    // Cuentas de correo saliente (cada una con su SMTP/auth). Una queda como activa.
    public List<CuentaCorreo> CuentasCorreo { get; set; } = new();

    /// <summary>Cuenta de correo activa (la que la app usa para enviar). Null si no hay ninguna marcada.</summary>
    [NotMapped]
    public CuentaCorreo? CuentaCorreoActiva => CuentasCorreo.FirstOrDefault(c => c.Activo);

    // ── Plantilla del mail saliente (global; ver PuertoBB.Core.Mail.PlantillaMail) ──
    public string? MailAsunto       { get; set; }
    public string? MailCuerpo       { get; set; }   // texto plano u HTML según MailCuerpoEsHtml
    public bool    MailCuerpoEsHtml { get; set; }   // false = texto plano (default)
}
