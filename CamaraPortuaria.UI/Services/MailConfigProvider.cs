using PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Mail;

namespace CamaraPortuaria.UI.Services;

public class MailConfigProvider : IMailConfigProvider
{
    private readonly IConfiguracionRepository _config;
    public MailConfigProvider(IConfiguracionRepository config) => _config = config;

    public async Task<MailConfig> GetAsync(CancellationToken ct = default)
    {
        var c = await _config.GetAsync(ct);
        return new MailConfig
        {
            SmtpHost       = c.SmtpHost,
            SmtpPort       = c.SmtpPort,
            SmtpSeguridad  = (PuertoBB.Core.Models.Mail.SmtpSeguridad)c.SmtpSeguridad,
            SmtpUsuario    = c.SmtpUsuario,
            SmtpPassword   = c.SmtpPassword,
            EmailRemitente = c.EmailRemitente,
            NombreRemitente = string.IsNullOrWhiteSpace(c.RazonSocial) ? "Cámara Portuaria" : c.RazonSocial
        };
    }
}
