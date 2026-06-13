namespace PuertoBB.Core.Models.Mail;

/// <summary>Resultado del consentimiento interactivo OAuth2: el refresh token a persistir y el email
/// de la cuenta autenticada (login XOAUTH2).</summary>
public record OAuthResultado(string RefreshToken, string? Usuario);
