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

    /// <summary>
    /// Genera un backup en la carpeta automática (<see cref="CarpetaBackups"/>), verifica la copia
    /// recién creada con integrity_check y rota las viejas (conserva las últimas N). Devuelve la ruta
    /// del backup generado. Se usa en el arranque (copia diaria) y antes de operaciones de riesgo.
    /// </summary>
    Task<ServiceResult<string>> BackupAutomaticoAsync(CancellationToken ct = default);

    /// <summary>
    /// Abre un archivo <c>.db</c> externo en solo lectura y corre <c>PRAGMA integrity_check</c>;
    /// devuelve "ok" o el detalle de los problemas. Si <paramref name="validarEsDeEstaApp"/> es true,
    /// además valida que el archivo sea una base de esta aplicación (tiene la tabla esperada) y, si no,
    /// devuelve un error. Sirve para verificar una copia recién creada y para validar antes de restaurar.
    /// </summary>
    Task<ServiceResult<string>> VerificarArchivoAsync(string path, bool validarEsDeEstaApp = false, CancellationToken ct = default);

    /// <summary>Carpeta donde se guardan los backups automáticos (se crea si no existe).</summary>
    string CarpetaBackups();

    /// <summary>Fecha del backup automático más reciente, o null si no hay ninguno.</summary>
    DateTime? FechaUltimoBackup();
}
