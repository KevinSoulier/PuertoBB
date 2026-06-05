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
}
