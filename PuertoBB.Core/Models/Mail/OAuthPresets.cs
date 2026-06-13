namespace PuertoBB.Core.Models.Mail;

/// <summary>Endpoints + scope resueltos para un proveedor/flujo OAuth2.</summary>
public readonly record struct OAuthEndpoints(string AuthorizeEndpoint, string TokenEndpoint, string Scope);

/// <summary>Sugerencia de transporte SMTP para un proveedor (la UI la usa para autocompletar).</summary>
public readonly record struct SmtpSugerencia(string Host, int Puerto, SmtpSeguridad Seguridad);

/// <summary>Tabla de presets OAuth2 por proveedor. Única fuente de endpoints/scopes; unidad pura testeable.</summary>
public static class OAuthPresets
{
    // outlook.office.com/SMTP.Send sirve para cuentas personales (Outlook.com) y de empresa (M365);
    // outlook.office365.com lo rechazan las cuentas personales. El cliente (app-only) es solo empresa.
    private const string ScopeMicrosoftInteractivo = "https://outlook.office.com/SMTP.Send offline_access openid email";
    private const string ScopeMicrosoftCliente     = "https://outlook.office365.com/.default";
    private const string ScopeGoogle               = "https://mail.google.com/ openid email";

    /// <summary>Resuelve los endpoints y el scope efectivos. Un <see cref="MailConfig.OAuthScope"/> no vacío
    /// tiene prioridad sobre el scope por defecto del proveedor.</summary>
    public static OAuthEndpoints Resolver(MailConfig config)
    {
        var (authorize, token, scopeDefecto) = config.OAuthProveedor switch
        {
            OAuthProveedor.Microsoft or OAuthProveedor.OutlookPersonal => (
                $"https://login.microsoftonline.com/{Tenant(config)}/oauth2/v2.0/authorize",
                $"https://login.microsoftonline.com/{Tenant(config)}/oauth2/v2.0/token",
                config.OAuthFlujo == OAuthFlujo.Cliente ? ScopeMicrosoftCliente : ScopeMicrosoftInteractivo),
            OAuthProveedor.Google => (
                "https://accounts.google.com/o/oauth2/v2/auth",
                "https://oauth2.googleapis.com/token",
                ScopeGoogle),
            _ /* Personalizado */ => (
                config.OAuthAuthorizeEndpoint ?? string.Empty,
                config.OAuthTokenEndpoint ?? string.Empty,
                config.OAuthScope ?? string.Empty)
        };

        var scope = string.IsNullOrWhiteSpace(config.OAuthScope) ? scopeDefecto : config.OAuthScope.Trim();
        return new OAuthEndpoints(authorize, token, scope);
    }

    /// <summary>Tenant para Microsoft: el configurado o «common» (cuentas personales + de empresa).</summary>
    public static string Tenant(MailConfig config) =>
        string.IsNullOrWhiteSpace(config.OAuthTenantId) ? "common" : config.OAuthTenantId.Trim();

    /// <summary>Sugerencia de host/puerto/seguridad SMTP por proveedor (null para Personalizado).</summary>
    public static SmtpSugerencia? SugerenciaSmtp(OAuthProveedor proveedor) => proveedor switch
    {
        OAuthProveedor.Microsoft      => new SmtpSugerencia("smtp.office365.com", 587, SmtpSeguridad.Auto),
        OAuthProveedor.OutlookPersonal => new SmtpSugerencia("smtp-mail.outlook.com", 587, SmtpSeguridad.Auto),
        OAuthProveedor.Google         => new SmtpSugerencia("smtp.gmail.com", 587, SmtpSeguridad.Auto),
        _                             => null
    };
}
