using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using PuertoBB.Core.Common;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Mail;

namespace PuertoBB.Services.Mail;

/// <summary>Envío de comprobantes por SMTP con MailKit. Lee la config vigente de la app.</summary>
public class MailService : IMailService
{
    // MailKit aplica este timeout por operación de socket. El default (2 min) se queda corto
    // subiendo adjuntos grandes (PDF consolidado de varios MB) por conexiones lentas.
    private const int SmtpTimeoutMs = 300_000; // 5 min

    private readonly IMailConfigProvider _configProvider;
    private readonly IMailTokenProvider _tokenProvider;
    private readonly ILogger<MailService> _logger;

    public MailService(IMailConfigProvider configProvider, IMailTokenProvider tokenProvider, ILogger<MailService> logger)
    {
        _configProvider = configProvider;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    public async Task<ServiceResult<bool>> EnviarReciboAsync(
        IEnumerable<string> destinatarios,
        byte[] pdfAdjunto,
        string nombreAdjunto,
        string asunto,
        string cuerpo,
        string? cuerpoHtml = null,
        CancellationToken ct = default)
    {
        var config = await _configProvider.GetAsync(ct);
        if (!config.EstaConfigurado)
            return ServiceResult<bool>.Fail("El servidor de correo (SMTP) no está configurado.");

        // Remitente: validar formato antes de armar el mensaje (evita el críptico "Invalid addr-spec").
        if (!MailboxAddress.TryParse(config.EmailRemitente?.Trim(), out var fromAddress))
            return ServiceResult<bool>.Fail(
                $"El email remitente «{config.EmailRemitente}» no tiene un formato válido. Corregilo en Configuración → Correo.");
        fromAddress.Name = config.NombreRemitente;

        // Destinatarios: parsear cada uno; descartar inválidos y seguir con los válidos.
        var limpios = destinatarios.Where(d => !string.IsNullOrWhiteSpace(d)).Select(d => d.Trim()).Distinct().ToList();
        var validos = new List<MailboxAddress>();
        var invalidos = new List<string>();
        foreach (var d in limpios)
        {
            if (MailboxAddress.TryParse(d, out var addr)) validos.Add(addr);
            else invalidos.Add(d);
        }
        if (validos.Count == 0)
            return ServiceResult<bool>.Fail(invalidos.Count > 0
                ? $"Ninguna dirección de destino es válida (ej. «{invalidos[0]}»)."
                : "La entidad no tiene direcciones de email configuradas.");

        try
        {
            var mensaje = new MimeMessage();
            mensaje.From.Add(fromAddress);
            foreach (var addr in validos)
                mensaje.To.Add(addr);
            mensaje.Subject = asunto;

            var builder = new BodyBuilder { TextBody = cuerpo };
            if (!string.IsNullOrWhiteSpace(cuerpoHtml)) builder.HtmlBody = cuerpoHtml;
            builder.Attachments.Add(nombreAdjunto, pdfAdjunto, ContentType.Parse("application/pdf"));
            mensaje.Body = builder.ToMessageBody();

            using var client = new SmtpClient { Timeout = SmtpTimeoutMs };
            await client.ConnectAsync(config.SmtpHost!, config.SmtpPort, ResolverOpciones(config), ct);
            await AutenticarAsync(client, config, ct);
            await client.SendAsync(mensaje, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("Mail enviado a {Cantidad} destinatario(s): {Asunto}", validos.Count, asunto);
            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falló el envío de mail: {Asunto}", asunto);
            return ServiceResult<bool>.Fail($"No se pudo enviar el mail: {MailErrores.Describir(ex, ct.IsCancellationRequested)}");
        }
    }

    public async Task<ServiceResult<string>> ProbarConexionAsync(CancellationToken ct = default)
    {
        var config = await _configProvider.GetAsync(ct);
        return await ProbarConexionAsync(config, ct);
    }

    public async Task<ServiceResult<string>> ProbarConexionAsync(MailConfig config, CancellationToken ct = default)
    {
        if (!config.EstaConfigurado)
            return ServiceResult<string>.Fail("Configure el servidor y el email remitente antes de probar.");
        if (!MailboxAddress.TryParse(config.EmailRemitente?.Trim(), out _))
            return ServiceResult<string>.Fail(
                $"El email remitente «{config.EmailRemitente}» no tiene un formato válido. Corregilo en Configuración → Correo.");

        try
        {
            using var client = new SmtpClient { Timeout = SmtpTimeoutMs };
            await client.ConnectAsync(config.SmtpHost!, config.SmtpPort, ResolverOpciones(config), ct);
            await AutenticarAsync(client, config, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("Prueba SMTP OK: {Host}:{Port}", config.SmtpHost, config.SmtpPort);
            return ServiceResult<string>.Ok($"Conexión correcta con {config.SmtpHost}:{config.SmtpPort}.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falló prueba SMTP: {Host}:{Port}", config.SmtpHost, config.SmtpPort);
            return ServiceResult<string>.Fail($"No se pudo conectar: {MailErrores.Describir(ex, ct.IsCancellationRequested)}");
        }
    }

    /// <summary>Autentica el cliente SMTP según el modo configurado. Lanza si OAuth2 no puede obtener token.</summary>
    private async Task AutenticarAsync(SmtpClient client, MailConfig config, CancellationToken ct)
    {
        switch (config.Autenticacion)
        {
            case MailAutenticacion.Ninguna:
                return;

            case MailAutenticacion.OAuth2:
                var token = await _tokenProvider.GetAccessTokenAsync(config, ct);
                if (!token.Success)
                    throw new InvalidOperationException(token.ErrorMessage ?? "No se pudo obtener el token OAuth2.");
                var usuario = config.OAuthUsuario ?? config.SmtpUsuario ?? config.EmailRemitente;
                if (string.IsNullOrWhiteSpace(usuario))
                    throw new InvalidOperationException("Falta el email de la cuenta para autenticar por OAuth2.");
                await client.AuthenticateAsync(new SaslMechanismOAuth2(usuario, token.Data!), ct);
                return;

            case MailAutenticacion.Basica:
            default:
                // Comportamiento histórico: autenticar solo si hay usuario (algunos relays no lo exigen).
                if (!string.IsNullOrWhiteSpace(config.SmtpUsuario))
                    await client.AuthenticateAsync(config.SmtpUsuario, config.SmtpPassword ?? string.Empty, ct);
                return;
        }
    }

    private static SecureSocketOptions ResolverOpciones(MailConfig config) => config.SmtpSeguridad switch
    {
        SmtpSeguridad.SslOnConnect => SecureSocketOptions.SslOnConnect,
        SmtpSeguridad.None         => SecureSocketOptions.None,
        _                          => SecureSocketOptions.StartTlsWhenAvailable
    };
}
