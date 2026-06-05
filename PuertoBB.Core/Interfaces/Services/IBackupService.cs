using PuertoBB.Core.Common;

namespace PuertoBB.Core.Interfaces.Services;

/// <summary>Backup manual de la base SQLite a una ruta elegida por el usuario.</summary>
public interface IBackupService
{
    Task<ServiceResult<bool>> BackupAsync(string destinoPath, CancellationToken ct = default);

    /// <summary>Nombre de archivo sugerido para el backup (incluye fecha).</summary>
    string NombreSugerido();
}
