using PuertoBB.Core.Common;

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
        CancellationToken ct = default);

    /// <summary>Verifica la conexión SMTP sin enviar ningún mensaje. Retorna el banner del servidor si OK.</summary>
    Task<ServiceResult<string>> ProbarConexionAsync(CancellationToken ct = default);
}
