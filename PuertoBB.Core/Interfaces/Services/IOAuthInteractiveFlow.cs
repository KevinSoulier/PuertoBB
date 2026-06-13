using PuertoBB.Core.Common;
using PuertoBB.Core.Models.Mail;

namespace PuertoBB.Core.Interfaces.Services;

/// <summary>Ejecuta el consentimiento interactivo OAuth2 (authorization code + PKCE con redirect a loopback)
/// y devuelve el refresh token + el email autenticado para persistir en la configuración.</summary>
public interface IOAuthInteractiveFlow
{
    Task<ServiceResult<OAuthResultado>> AutenticarAsync(MailConfig config, CancellationToken ct = default);
}
