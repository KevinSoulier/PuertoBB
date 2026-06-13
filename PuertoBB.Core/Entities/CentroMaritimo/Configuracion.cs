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
    public int DiasVencimiento { get; set; } = 30;

    // Mail saliente
    public string? SmtpHost       { get; set; }
    public int     SmtpPort       { get; set; }
    public int     SmtpSeguridad  { get; set; } = 0; // 0=Auto, 1=SslOnConnect, 2=None
    public string? SmtpUsuario    { get; set; }
    public string? SmtpPassword   { get; set; }
    public string? EmailRemitente { get; set; }

    // Autenticación de correo (ver PuertoBB.Core.Models.Mail)
    public int Autenticacion  { get; set; } = 1; // 0=Ninguna, 1=Basica, 2=OAuth2
    public int OAuthProveedor { get; set; } = 0; // 0=Microsoft, 1=Google, 2=Personalizado
    public int OAuthFlujo     { get; set; } = 0; // 0=Interactivo, 1=Cliente
    public string? OAuthClientId          { get; set; }
    public string? OAuthClientSecret      { get; set; }
    public string? OAuthTenantId          { get; set; }
    public string? OAuthScope             { get; set; }
    public string? OAuthAuthorizeEndpoint { get; set; }
    public string? OAuthTokenEndpoint     { get; set; }
    public string? OAuthRefreshToken      { get; set; } // texto plano (ver D-24)
    public string? OAuthUsuario           { get; set; }
}
