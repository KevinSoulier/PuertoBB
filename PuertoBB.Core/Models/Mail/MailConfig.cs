namespace PuertoBB.Core.Models.Mail;

/// <summary>Configuración SMTP saliente. La provee cada app desde su Configuracion.</summary>
public record MailConfig
{
    public string?       SmtpHost        { get; init; }
    public int           SmtpPort        { get; init; }
    public string?       SmtpUsuario     { get; init; }
    public string?       SmtpPassword    { get; init; }
    public string?       EmailRemitente  { get; init; }
    public string        NombreRemitente { get; init; } = "PuertoBB";
    public SmtpSeguridad SmtpSeguridad   { get; init; } = SmtpSeguridad.Auto;

    // ── Autenticación ──
    public MailAutenticacion Autenticacion  { get; init; } = MailAutenticacion.Basica;

    // ── OAuth2 (solo cuando Autenticacion == OAuth2) ──
    public OAuthProveedor OAuthProveedor { get; init; } = OAuthProveedor.Microsoft;
    public OAuthFlujo     OAuthFlujo     { get; init; } = OAuthFlujo.Interactivo;
    public string? OAuthClientId          { get; init; }
    public string? OAuthClientSecret      { get; init; }
    public string? OAuthTenantId          { get; init; }
    public string? OAuthScope             { get; init; }
    /// <summary>Solo para OAuthProveedor.Personalizado.</summary>
    public string? OAuthAuthorizeEndpoint { get; init; }
    /// <summary>Solo para OAuthProveedor.Personalizado.</summary>
    public string? OAuthTokenEndpoint     { get; init; }
    /// <summary>Refresh token obtenido por el flujo interactivo (texto plano, ver D-24).</summary>
    public string? OAuthRefreshToken      { get; init; }
    /// <summary>Email autenticado: login XOAUTH2 (interactivo) o casilla «send as» (cliente).</summary>
    public string? OAuthUsuario           { get; init; }

    /// <summary>Hay lo mínimo para intentar enviar (servidor + remitente). La validación de credenciales
    /// por modo está en <see cref="Validar"/>.</summary>
    public bool EstaConfigurado =>
        !string.IsNullOrWhiteSpace(SmtpHost) && !string.IsNullOrWhiteSpace(EmailRemitente);

    /// <summary>Valida la configuración según el modo de autenticación. Devuelve null si es válida,
    /// o un mensaje accionable para mostrar en la UI.</summary>
    public string? Validar()
    {
        if (string.IsNullOrWhiteSpace(SmtpHost))       return "Indicá el servidor SMTP.";
        if (string.IsNullOrWhiteSpace(EmailRemitente)) return "Indicá el email remitente.";

        switch (Autenticacion)
        {
            case MailAutenticacion.Ninguna:
                return null;

            case MailAutenticacion.Basica:
                return string.IsNullOrWhiteSpace(SmtpUsuario)
                    ? "Indicá el usuario para la autenticación básica."
                    : null;

            case MailAutenticacion.OAuth2:
                if (string.IsNullOrWhiteSpace(OAuthClientId))
                    return "Indicá el Client ID de OAuth2.";
                if (OAuthProveedor == OAuthProveedor.Personalizado)
                {
                    if (string.IsNullOrWhiteSpace(OAuthTokenEndpoint))
                        return "Indicá el endpoint de token (OAuth2 personalizado).";
                    if (string.IsNullOrWhiteSpace(OAuthScope))
                        return "Indicá el scope (OAuth2 personalizado).";
                    if (OAuthFlujo == OAuthFlujo.Interactivo && string.IsNullOrWhiteSpace(OAuthAuthorizeEndpoint))
                        return "Indicá el endpoint de autorización (OAuth2 personalizado interactivo).";
                }
                return OAuthFlujo switch
                {
                    OAuthFlujo.Cliente => string.IsNullOrWhiteSpace(OAuthClientSecret)
                        ? "Indicá el Client Secret para el flujo de cliente."
                        : string.IsNullOrWhiteSpace(OAuthTenantId) && OAuthProveedor == OAuthProveedor.Microsoft
                            ? "Indicá el Tenant ID para el flujo de cliente de Microsoft."
                            : null,
                    _ /* Interactivo */ => string.IsNullOrWhiteSpace(OAuthRefreshToken)
                        ? "Iniciá sesión con «Iniciar sesión…» para autorizar el envío."
                        : null
                };

            default:
                return null;
        }
    }
}
