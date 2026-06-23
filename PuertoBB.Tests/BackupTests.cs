using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Tests.TestSupport;
using Xunit;

namespace PuertoBB.Tests;

/// <summary>
/// Valida las operaciones SQL del backup/verificación (VACUUM INTO, integrity_check, tabla centinela)
/// sobre SQLite real. El BackupService concreto vive en los proyectos WPF (no referenciables desde
/// aquí), pero ejecuta exactamente este mismo SQL.
/// </summary>
public class BackupTests
{
    [Fact]
    public async Task VacuumInto_GeneraCopiaConsultable()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        db.Barcos.Add(new Barco { Nombre = "Backup Test", CreatedAt = DateTime.Now });
        await db.SaveChangesAsync();

        var destino = TempDbPath();
        try
        {
            await VacuumIntoAsync(db, destino);

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
        finally { LimpiarTemp(destino); }
    }

    [Fact]
    public async Task IntegrityCheck_CopiaSana_DevuelveOk()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        var destino = TempDbPath();
        try
        {
            await VacuumIntoAsync(db, destino);
            Assert.Equal("ok", IntegrityCheck(destino));
        }
        finally { LimpiarTemp(destino); }
    }

    [Fact]
    public async Task TablaCentinela_DistingueLaApp()
    {
        // El backup del Centro tiene "Barcos" pero no "Empresas"; el de la Cámara, al revés.
        // Es la validación que evita restaurar el backup de una app en la otra.
        var centro = TempDbPath();
        var camara = TempDbPath();
        try
        {
            using (var fxCentro = SqliteTestDb.CreateCentro(out var dbCentro))
                await VacuumIntoAsync(dbCentro, centro);
            using (var fxCamara = SqliteTestDb.CreateCamara(out var dbCamara))
                await VacuumIntoAsync(dbCamara, camara);

            Assert.True(TablaExiste(centro, "Barcos"));
            Assert.False(TablaExiste(centro, "Empresas"));
            Assert.True(TablaExiste(camara, "Empresas"));
            Assert.False(TablaExiste(camara, "Barcos"));
        }
        finally { LimpiarTemp(centro); LimpiarTemp(camara); }
    }

    [Fact]
    public void IntegrityCheck_ArchivoCorrupto_Falla()
    {
        // Un archivo con bytes basura no es una base SQLite válida → integrity_check debe fallar
        // (SQLITE_NOTADB). Así el verificador rechaza un backup dañado antes de restaurarlo.
        var corrupto = TempDbPath();
        File.WriteAllText(corrupto, "esto no es una base de datos SQLite");
        try
        {
            Assert.Throws<SqliteException>(() => IntegrityCheck(corrupto));
        }
        finally { LimpiarTemp(corrupto); }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    private static string TempDbPath() => Path.Combine(Path.GetTempPath(), $"pbb-backup-{Guid.NewGuid():N}.db");

    private static Task VacuumIntoAsync(DbContext db, string destino)
    {
        if (File.Exists(destino)) File.Delete(destino);
        var sql = "VACUUM INTO '" + destino.Replace("'", "''") + "'";
        return db.Database.ExecuteSqlRawAsync(sql);
    }

    private static string IntegrityCheck(string path)
    {
        using var conn = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check";
        using var reader = cmd.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read()) rows.Add(reader.GetString(0));
        return string.Join("\n", rows);
    }

    private static bool TablaExiste(string path, string tabla)
    {
        using var conn = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$t";
        cmd.Parameters.AddWithValue("$t", tabla);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    private static void LimpiarTemp(string path)
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(path)) File.Delete(path);
    }
}
