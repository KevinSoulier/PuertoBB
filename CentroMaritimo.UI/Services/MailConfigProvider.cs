using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Mail;

namespace CentroMaritimo.UI.Services;

public class MailConfigProvider : IMailConfigProvider
{
    private readonly IConfiguracionRepository _config;
    public MailConfigProvider(IConfiguracionRepository config) => _config = config;

    public async Task<MailConfig> GetAsync(CancellationToken ct = default)
    {
        var c = await _config.GetAsync(ct);
        return new MailConfig
        {
            SmtpHost = c.SmtpHost,
            SmtpPort = c.SmtpPort,
            SmtpUsuario = c.SmtpUsuario,
            SmtpPassword = c.SmtpPassword,
            EmailRemitente = c.EmailRemitente,
            NombreRemitente = string.IsNullOrWhiteSpace(c.RazonSocial) ? "Centro Marítimo" : c.RazonSocial
        };
    }
}
