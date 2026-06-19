using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Mail;

namespace CentroMaritimo.UI.Services;

public class MailConfigProvider : IMailConfigProvider
{
    private readonly IConfiguracionRepository _config;

    public MailConfigProvider(IConfiguracionRepository config)
    {
        _config = config;
    }

    public async Task<MailConfig> GetAsync(CancellationToken ct = default)
    {
        var c = await _config.GetSinTrackingAsync(ct);
        var nombreRemitente = string.IsNullOrWhiteSpace(c.RazonSocial) ? "Centro Marítimo" : c.RazonSocial;
        var cuenta = c.CuentaCorreoActiva;
        if (cuenta is null)
            return new MailConfig { NombreRemitente = nombreRemitente }; // sin cuenta activa → EstaConfigurado=false

        return new MailConfig
        {
            SmtpHost        = cuenta.SmtpHost,
            SmtpPort        = cuenta.SmtpPort,
            SmtpSeguridad   = (PuertoBB.Core.Models.Mail.SmtpSeguridad)cuenta.SmtpSeguridad,
            SmtpUsuario     = cuenta.SmtpUsuario,
            SmtpPassword    = cuenta.SmtpPassword,
            EmailRemitente  = cuenta.EmailRemitente,
            NombreRemitente = nombreRemitente,
            Autenticacion   = (MailAutenticacion)cuenta.Autenticacion,
            OAuthProveedor  = (OAuthProveedor)cuenta.OAuthProveedor,
            OAuthFlujo      = (OAuthFlujo)cuenta.OAuthFlujo,
            OAuthClientId          = cuenta.OAuthClientId,
            OAuthClientSecret      = cuenta.OAuthClientSecret,
            OAuthTenantId          = cuenta.OAuthTenantId,
            OAuthScope             = cuenta.OAuthScope,
            OAuthAuthorizeEndpoint = cuenta.OAuthAuthorizeEndpoint,
            OAuthTokenEndpoint     = cuenta.OAuthTokenEndpoint,
            OAuthRefreshToken      = cuenta.OAuthRefreshToken,
            OAuthUsuario           = cuenta.OAuthUsuario
        };
    }
}
