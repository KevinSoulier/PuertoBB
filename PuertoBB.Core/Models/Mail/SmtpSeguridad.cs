namespace PuertoBB.Core.Models.Mail;

public enum SmtpSeguridad
{
    Auto = 0,        // Port-aware: 465 → SSL implícito; 587/25 → STARTTLS (lo decide MailKit)
    SslOnConnect = 1, // SSL implícito (puerto 465)
    None = 2          // Sin cifrado (SMTP interno sin TLS)
}
