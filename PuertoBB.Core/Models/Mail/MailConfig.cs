namespace PuertoBB.Core.Models.Mail;

/// <summary>Configuración SMTP saliente. La provee cada app desde su Configuracion.</summary>
public record MailConfig
{
    public string? SmtpHost       { get; init; }
    public int     SmtpPort       { get; init; }
    public string? SmtpUsuario    { get; init; }
    public string? SmtpPassword   { get; init; }
    public string? EmailRemitente { get; init; }
    public string  NombreRemitente { get; init; } = "PuertoBB";

    public bool EstaConfigurado =>
        !string.IsNullOrWhiteSpace(SmtpHost) && !string.IsNullOrWhiteSpace(EmailRemitente);
}
