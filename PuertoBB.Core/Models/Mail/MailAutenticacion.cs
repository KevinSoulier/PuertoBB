namespace PuertoBB.Core.Models.Mail;

/// <summary>Modo de autenticación SMTP saliente.</summary>
public enum MailAutenticacion
{
    /// <summary>Sin autenticación (relay interno que no la exige).</summary>
    Ninguna = 0,
    /// <summary>Usuario + contraseña (AUTH LOGIN/PLAIN). Cubre Gmail con contraseña de aplicación,
    /// Brevo/SendGrid/SES (API key como contraseña), Yahoo, Zoho y SMTP propios.</summary>
    Basica = 1,
    /// <summary>OAuth2 (SASL XOAUTH2). Necesario para Microsoft 365/Outlook, que retiró la auth básica.</summary>
    OAuth2 = 2
}
