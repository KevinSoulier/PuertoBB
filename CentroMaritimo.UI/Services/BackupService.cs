using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Common;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Infrastructure.Data;

namespace CentroMaritimo.UI.Services;

/// <summary>
/// Backup de la base del Centro Marítimo usando "VACUUM INTO" (copia consistente, sin bloquear).
/// </summary>
public class BackupService : IBackupService
{
    private readonly CentroMaritimoDbContext _db;
    private readonly ILogger<BackupService> _logger;

    public BackupService(CentroMaritimoDbContext db, ILogger<BackupService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public string NombreSugerido() => $"centro-maritimo-backup-{DateTime.Now:yyyyMMdd-HHmm}.db";

    public async Task<ServiceResult<bool>> BackupAsync(string destinoPath, CancellationToken ct = default)
    {
        try
        {
            // VACUUM INTO no admite parámetros enlazados; se escapan las comillas simples.
            // La ruta la elige el usuario con SaveFileDialog (no es entrada externa).
            var sql = "VACUUM INTO '" + destinoPath.Replace("'", "''") + "'";
            await _db.Database.ExecuteSqlRawAsync(sql, ct);
            _logger.LogInformation("Backup generado en {Ruta}", destinoPath);
            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló el backup a {Ruta}", destinoPath);
            return ServiceResult<bool>.Fail($"No se pudo generar el backup: {ex.Message}");
        }
    }
}
