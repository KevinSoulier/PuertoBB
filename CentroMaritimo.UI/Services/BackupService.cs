using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Common;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Infrastructure.Data;

namespace CentroMaritimo.UI.Services;

/// <summary>
/// Backup, restauración y mantenimiento de la base del Centro Marítimo.
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
            // VACUUM INTO falla si el archivo destino ya existe (P2-9).
            if (File.Exists(destinoPath)) File.Delete(destinoPath);
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

    public async Task<ServiceResult<bool>> RestaurarAsync(string origenPath, CancellationToken ct = default)
    {
        try
        {
            var conn = (SqliteConnection)_db.Database.GetDbConnection();
            var dbPath = conn.DataSource;

            _db.Database.CloseConnection();
            // Limpiar el pool para liberar todos los handles abiertos por otros DbContext transient (P1-5).
            SqliteConnection.ClearAllPools();
            await Task.Run(() => File.Copy(origenPath, dbPath, overwrite: true), ct);
            _logger.LogInformation("Base restaurada desde {Origen}", origenPath);
            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la restauración desde {Origen}", origenPath);
            return ServiceResult<bool>.Fail($"No se pudo restaurar el backup: {ex.Message}");
        }
    }

    public async Task<ServiceResult<string>> VerificarIntegridadAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.Database.OpenConnectionAsync(ct);
            var conn = _db.Database.GetDbConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check";
            using var reader = await cmd.ExecuteReaderAsync(ct);
            var rows = new List<string>();
            while (await reader.ReadAsync(ct))
                rows.Add(reader.GetString(0));
            var resultado = string.Join("\n", rows);
            _logger.LogInformation("integrity_check: {Resultado}", resultado);
            return ServiceResult<string>.Ok(resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la verificación de integridad");
            return ServiceResult<string>.Fail($"No se pudo verificar la integridad: {ex.Message}");
        }
    }

    public async Task<ServiceResult<bool>> VacuumAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.Database.ExecuteSqlRawAsync("VACUUM", ct);
            _logger.LogInformation("VACUUM ejecutado correctamente");
            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló el VACUUM");
            return ServiceResult<bool>.Fail($"No se pudo compactar la base de datos: {ex.Message}");
        }
    }

    public async Task<ServiceResult<bool>> OptimizarAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.Database.ExecuteSqlRawAsync("PRAGMA optimize", ct);
            _logger.LogInformation("PRAGMA optimize ejecutado correctamente");
            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló PRAGMA optimize");
            return ServiceResult<bool>.Fail($"No se pudo optimizar la base de datos: {ex.Message}");
        }
    }
}
