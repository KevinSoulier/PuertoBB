using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using PuertoBB.Core.Common;
using PuertoBB.Core.Interfaces.Services;

namespace PuertoBB.Services.Mail;

/// <summary>Envío de comprobantes por SMTP con MailKit. Lee la config vigente de la app.</summary>
public class MailService : IMailService
{
    private readonly IMailConfigProvider _configProvider;
    private readonly ILogger<MailService> _logger;

    public MailService(IMailConfigProvider configProvider, ILogger<MailService> logger)
    {
        _configProvider = configProvider;
        _logger = logger;
    }

    public async Task<ServiceResult<bool>> EnviarReciboAsync(
        IEnumerable<string> destinatarios,
        byte[] pdfAdjunto,
        string nombreAdjunto,
        string asunto,
        string cuerpo,
        CancellationToken ct = default)
    {
        var dest = destinatarios.Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().ToList();
        if (dest.Count == 0)
            return ServiceResult<bool>.Fail("La entidad no tiene direcciones de email configuradas.");

        var config = await _configProvider.GetAsync(ct);
        if (!config.EstaConfigurado)
            return ServiceResult<bool>.Fail("El servidor de correo (SMTP) no está configurado.");

        try
        {
            var mensaje = new MimeMessage();
            mensaje.From.Add(new MailboxAddress(config.NombreRemitente, config.EmailRemitente!)); // EstaConfigurado garantiza no-null
            foreach (var d in dest)
                mensaje.To.Add(MailboxAddress.Parse(d));
            mensaje.Subject = asunto;

            var builder = new BodyBuilder { TextBody = cuerpo };
            builder.Attachments.Add(nombreAdjunto, pdfAdjunto, ContentType.Parse("application/pdf"));
            mensaje.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            var socketOptions = config.SmtpPort == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTlsWhenAvailable;

            await client.ConnectAsync(config.SmtpHost!, config.SmtpPort, socketOptions, ct); // EstaConfigurado garantiza no-null
            if (!string.IsNullOrWhiteSpace(config.SmtpUsuario))
                await client.AuthenticateAsync(config.SmtpUsuario, config.SmtpPassword ?? string.Empty, ct);
            await client.SendAsync(mensaje, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("Mail enviado a {Cantidad} destinatario(s): {Asunto}", dest.Count, asunto);
            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falló el envío de mail: {Asunto}", asunto);
            return ServiceResult<bool>.Fail($"No se pudo enviar el mail: {ex.Message}");
        }
    }
}
