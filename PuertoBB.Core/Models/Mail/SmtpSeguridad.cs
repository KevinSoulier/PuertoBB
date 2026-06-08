namespace PuertoBB.Core.Models.Mail;

public enum SmtpSeguridad
{
    Auto = 0,        // StartTlsWhenAvailable — cubre Gmail, Outlook, Yahoo
    SslOnConnect = 1, // SSL implícito (puerto 465)
    None = 2          // Sin cifrado (SMTP interno sin TLS)
}
