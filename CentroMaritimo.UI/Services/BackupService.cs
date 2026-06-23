using System.IO;
using System.Linq;
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
    /// <summary>Cantidad de backups automáticos que se conservan (los más viejos se borran por rotación).</summary>
    private const int MaxBackupsAutomaticos = 10;

    /// <summary>Tabla propia del Centro Marítimo (no existe en la Cámara): valida que un backup sea de esta app.</summary>
    private const string TablaCentinela = "Barcos";

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

    public string CarpetaBackups()
    {
        var dbPath = ((SqliteConnection)_db.Database.GetDbConnection()).DataSource;
        var dir = Path.Combine(Path.GetDirectoryName(dbPath)!, "Backups");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public DateTime? FechaUltimoBackup()
    {
        try
        {
            var archivos = Directory.GetFiles(CarpetaBackups(), "*.db");
            return archivos.Length == 0 ? null : archivos.Max(File.GetLastWriteTime);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo determinar la fecha del último backup");
            return null;
        }
    }

    public async Task<ServiceResult<string>> BackupAutomaticoAsync(CancellationToken ct = default)
    {
        try
        {
            var destino = Path.Combine(CarpetaBackups(), NombreSugerido());
            var res = await BackupAsync(destino, ct);
            if (!res.Success)
                return ServiceResult<string>.Fail(res.ErrorMessage ?? "No se pudo generar el backup.");

            // Verificar la copia recién creada antes de confiar en ella y rotar las viejas.
            var verif = await VerificarArchivoAsync(destino, validarEsDeEstaApp: false, ct);
            if (!verif.Success || verif.Data != "ok")
            {
                _logger.LogWarning("El backup automático {Ruta} no pasó la verificación: {Detalle}",
                    destino, verif.Data ?? verif.ErrorMessage);
                return ServiceResult<string>.Fail("El backup se generó pero no pasó la verificación de integridad.");
            }

            RotarBackups();
            _logger.LogInformation("Backup automático generado y verificado en {Ruta}", destino);
            return ServiceResult<string>.Ok(destino);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló el backup automático");
            return ServiceResult<string>.Fail($"No se pudo generar el backup automático: {ex.Message}");
        }
    }

    /// <summary>Conserva los <see cref="MaxBackupsAutomaticos"/> backups más recientes y borra el resto.</summary>
    private void RotarBackups()
    {
        try
        {
            var sobrantes = Directory.GetFiles(CarpetaBackups(), "*.db")
                .OrderByDescending(File.GetLastWriteTime)
                .Skip(MaxBackupsAutomaticos)
                .ToList();
            foreach (var f in sobrantes)
            {
                File.Delete(f);
                _logger.LogInformation("Backup viejo eliminado por rotación: {Ruta}", f);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudieron rotar los backups viejos");
        }
    }

    public async Task<ServiceResult<string>> VerificarArchivoAsync(
        string path, bool validarEsDeEstaApp = false, CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(path))
                return ServiceResult<string>.Fail("El archivo de backup no existe.");

            // Pooling=False para no dejar un handle abierto sobre el archivo (luego se puede copiar/borrar).
            await using var conn = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
            await conn.OpenAsync(ct);

            if (validarEsDeEstaApp)
            {
                using var check = conn.CreateCommand();
                check.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$t";
                check.Parameters.AddWithValue("$t", TablaCentinela);
                var esDeEstaApp = Convert.ToInt64(await check.ExecuteScalarAsync(ct)) > 0;
                if (!esDeEstaApp)
                    return ServiceResult<string>.Fail(
                        "El archivo seleccionado no parece una base de datos de esta aplicación (Centro Marítimo).");
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check";
            using var reader = await cmd.ExecuteReaderAsync(ct);
            var rows = new List<string>();
            while (await reader.ReadAsync(ct))
                rows.Add(reader.GetString(0));
            return ServiceResult<string>.Ok(string.Join("\n", rows));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la verificación del archivo {Ruta}", path);
            return ServiceResult<string>.Fail($"No se pudo verificar el archivo: {ex.Message}");
        }
    }
}
