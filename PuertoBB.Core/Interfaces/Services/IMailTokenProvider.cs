using PuertoBB.Core.Common;
using PuertoBB.Core.Models.Mail;

namespace PuertoBB.Core.Interfaces.Services;

/// <summary>Obtiene un access token OAuth2 vigente para autenticar el SMTP (SASL XOAUTH2).
/// Cachea el token en memoria y lo renueva al expirar.</summary>
public interface IMailTokenProvider
{
    Task<ServiceResult<string>> GetAccessTokenAsync(MailConfig config, CancellationToken ct = default);
}
