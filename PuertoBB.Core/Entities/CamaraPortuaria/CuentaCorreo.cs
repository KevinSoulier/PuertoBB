using PuertoBB.Core.Entities.Common;

namespace PuertoBB.Core.Entities.CamaraPortuaria;

/// <summary>
/// Cuenta de correo saliente. Se pueden cargar varias (ej. "Ventas", "Administración") y una queda
/// marcada como <see cref="Activo"/>: la activa es la que usa la app para enviar. Espejo de PuntoDeVenta.
/// Secretos en texto plano (ver D-24).
/// </summary>
public class CuentaCorreo : BaseEntity
{
    public int ConfiguracionId { get; set; }

    /// <summary>Etiqueta para identificarla (ej. "Ventas", "Administración").</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Cuenta activa: la que usa la app para enviar. Solo una puede estar activa.</summary>
    public bool Activo { get; set; }

    // ── Transporte SMTP ──
    public string? SmtpHost       { get; set; }
    public int     SmtpPort       { get; set; }
    public int     SmtpSeguridad  { get; set; } = 0; // 0=Auto, 1=SslOnConnect, 2=None
    public string? EmailRemitente { get; set; }

    // ── Autenticación (ver PuertoBB.Core.Models.Mail) ──
    public int     Autenticacion { get; set; } = 1; // 0=Ninguna, 1=Básica, 2=OAuth2
    public string? SmtpUsuario   { get; set; }
    public string? SmtpPassword  { get; set; } // texto plano

    // ── OAuth2 ──
    public int OAuthProveedor { get; set; } = 0; // 0=Microsoft, 1=Google, 2=Personalizado, 3=OutlookPersonal
    public int OAuthFlujo     { get; set; } = 0; // 0=Interactivo, 1=Cliente
    public string? OAuthClientId          { get; set; }
    public string? OAuthClientSecret      { get; set; }
    public string? OAuthTenantId          { get; set; }
    public string? OAuthScope             { get; set; }
    public string? OAuthAuthorizeEndpoint { get; set; }
    public string? OAuthTokenEndpoint     { get; set; }
    public string? OAuthRefreshToken      { get; set; } // texto plano
    public string? OAuthUsuario           { get; set; }
}
