using PuertoBB.Core.Models.Mail;

namespace PuertoBB.Core.Interfaces.Services;

/// <summary>Provee la configuración SMTP vigente. Cada app la implementa leyendo su Configuracion.</summary>
public interface IMailConfigProvider
{
    Task<MailConfig> GetAsync(CancellationToken ct = default);
}
