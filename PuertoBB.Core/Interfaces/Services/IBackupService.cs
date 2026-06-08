using PuertoBB.Core.Common;

namespace PuertoBB.Core.Interfaces.Services;

/// <summary>Backup, restauración y mantenimiento de la base SQLite.</summary>
public interface IBackupService
{
    Task<ServiceResult<bool>> BackupAsync(string destinoPath, CancellationToken ct = default);
    Task<ServiceResult<bool>> RestaurarAsync(string origenPath, CancellationToken ct = default);
    Task<ServiceResult<string>> VerificarIntegridadAsync(CancellationToken ct = default);
    Task<ServiceResult<bool>> VacuumAsync(CancellationToken ct = default);
    Task<ServiceResult<bool>> OptimizarAsync(CancellationToken ct = default);

    /// <summary>Nombre de archivo sugerido para el backup (incluye fecha).</summary>
    string NombreSugerido();
}
