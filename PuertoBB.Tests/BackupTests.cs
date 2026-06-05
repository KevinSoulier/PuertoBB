using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Tests.TestSupport;
using Xunit;

namespace PuertoBB.Tests;

/// <summary>
/// Valida la operación SQL del backup (VACUUM INTO) sobre SQLite real. El BackupService concreto
/// vive en los proyectos WPF (no referenciables desde aquí), pero ejecuta exactamente este SQL.
/// </summary>
public class BackupTests
{
    [Fact]
    public async Task VacuumInto_GeneraCopiaConsultable()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        db.Barcos.Add(new Barco { Nombre = "Backup Test", CreatedAt = DateTime.Now });
        await db.SaveChangesAsync();

        var destino = Path.Combine(Path.GetTempPath(), $"pbb-backup-{Guid.NewGuid():N}.db");
        try
        {
            var sql = "VACUUM INTO '" + destino.Replace("'", "''") + "'";
            await db.Database.ExecuteSqlRawAsync(sql);

            Assert.True(File.Exists(destino));

            // La copia debe ser una base SQLite válida con los datos. Se cierra la conexión
            // (Pooling=False) antes del finally para poder borrar el archivo.
            int count;
            using (var conn = new SqliteConnection($"Data Source={destino};Pooling=False"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM Barcos";
                count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            Assert.Equal(1, count);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(destino)) File.Delete(destino);
        }
    }
}
