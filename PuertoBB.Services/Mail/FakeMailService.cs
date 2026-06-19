using Microsoft.Extensions.Logging;
using PuertoBB.Core.Common;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Mail;

namespace PuertoBB.Services.Mail;

/// <summary>
/// Implementación falsa de envío de mail para desarrollo/testing sin SMTP real.
/// Registra el envío y siempre devuelve éxito. Guarda los adjuntos en una carpeta temporal opcional.
/// </summary>
public class FakeMailService : IMailService
{
    private readonly ILogger<FakeMailService> _logger;
    private readonly string? _carpetaSalida;

    public FakeMailService(ILogger<FakeMailService> logger, string? carpetaSalida = null)
    {
        _logger = logger;
        _carpetaSalida = carpetaSalida;
    }

    /// <summary>Registro de los envíos realizados (útil para inspección en tests/UI de demo).</summary>
    public List<EnvioSimulado> Enviados { get; } = [];

    public async Task<ServiceResult<bool>> EnviarReciboAsync(
        IEnumerable<string> destinatarios,
        byte[] pdfAdjunto,
        string nombreAdjunto,
        string asunto,
        string cuerpo,
        string? cuerpoHtml = null,
        CancellationToken ct = default)
    {
        var dest = destinatarios.Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().ToList();
        if (dest.Count == 0)
            return ServiceResult<bool>.Fail("La entidad no tiene direcciones de email configuradas.");

        if (!string.IsNullOrWhiteSpace(_carpetaSalida))
        {
            Directory.CreateDirectory(_carpetaSalida);
            var ruta = Path.Combine(_carpetaSalida, nombreAdjunto);
            await File.WriteAllBytesAsync(ruta, pdfAdjunto, ct);
        }

        Enviados.Add(new EnvioSimulado(dest, nombreAdjunto, asunto, pdfAdjunto.Length));
        _logger.LogInformation("[FAKE] Mail simulado a {Dest}: {Asunto} ({Bytes} bytes)", string.Join(", ", dest), asunto, pdfAdjunto.Length);
        return ServiceResult<bool>.Ok(true);
    }

    public Task<ServiceResult<string>> ProbarConexionAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("[FAKE] Prueba de conexión SMTP simulada: OK");
        return Task.FromResult(ServiceResult<string>.Ok("Conexión simulada correctamente (modo demo)."));
    }

    public Task<ServiceResult<string>> ProbarConexionAsync(MailConfig config, CancellationToken ct = default)
    {
        _logger.LogInformation("[FAKE] Prueba de conexión SMTP simulada (config en edición): OK");
        return Task.FromResult(ServiceResult<string>.Ok("Conexión simulada correctamente (modo demo)."));
    }

    public record EnvioSimulado(IReadOnlyList<string> Destinatarios, string Adjunto, string Asunto, int Bytes);
}
