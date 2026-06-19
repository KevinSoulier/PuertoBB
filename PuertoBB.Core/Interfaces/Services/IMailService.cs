using PuertoBB.Core.Common;
using PuertoBB.Core.Models.Mail;

namespace PuertoBB.Core.Interfaces.Services;

/// <summary>Envío de comprobantes por mail (compartido). Solo varía el adjunto.</summary>
public interface IMailService
{
    Task<ServiceResult<bool>> EnviarReciboAsync(
        IEnumerable<string> destinatarios,
        byte[] pdfAdjunto,
        string nombreAdjunto,
        string asunto,
        string cuerpo,
        string? cuerpoHtml = null,
        CancellationToken ct = default);

    /// <summary>Verifica la conexión SMTP de la cuenta activa, sin enviar ningún mensaje. Retorna el banner del servidor si OK.</summary>
    Task<ServiceResult<string>> ProbarConexionAsync(CancellationToken ct = default);

    /// <summary>Verifica la conexión SMTP de la configuración indicada (p. ej. la cuenta en edición, antes de guardarla).</summary>
    Task<ServiceResult<string>> ProbarConexionAsync(MailConfig config, CancellationToken ct = default);
}
