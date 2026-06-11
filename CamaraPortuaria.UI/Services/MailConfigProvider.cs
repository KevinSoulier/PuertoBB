using PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Mail;
using PuertoBB.Services.Security;

namespace CamaraPortuaria.UI.Services;

public class MailConfigProvider : IMailConfigProvider
{
    private readonly IConfiguracionRepository _config;
    private readonly ISecretProtector _protector;

    public MailConfigProvider(IConfiguracionRepository config, ISecretProtector protector)
    {
        _config    = config;
        _protector = protector;
    }

    public async Task<MailConfig> GetAsync(CancellationToken ct = default)
    {
        var c = await _config.GetAsync(ct);
        return new MailConfig
        {
            SmtpHost        = c.SmtpHost,
            SmtpPort        = c.SmtpPort,
            SmtpSeguridad   = (PuertoBB.Core.Models.Mail.SmtpSeguridad)c.SmtpSeguridad,
            SmtpUsuario     = c.SmtpUsuario,
            SmtpPassword    = _protector.Unprotect(c.SmtpPassword),
            EmailRemitente  = c.EmailRemitente,
            NombreRemitente = string.IsNullOrWhiteSpace(c.RazonSocial) ? "Cámara Portuaria" : c.RazonSocial
        };
    }
}
