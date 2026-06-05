using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PuertoBB.Infrastructure.Data;

namespace PuertoBB.Tests.TestSupport;

/// <summary>
/// Crea contextos EF sobre SQLite in-memory. La conexión se mantiene abierta mientras viva
/// la instancia (al cerrarla, la base desaparece). Ejerce el mapeo EF real (índices únicos, etc.).
/// </summary>
public sealed class SqliteTestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    private SqliteTestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public static SqliteTestDb CreateCamara(out CamaraPortuariaDbContext db)
    {
        var fixture = new SqliteTestDb();
        var options = new DbContextOptionsBuilder<CamaraPortuariaDbContext>()
            .UseSqlite(fixture._connection).Options;
        db = new CamaraPortuariaDbContext(options);
        db.Database.EnsureCreated();
        return fixture;
    }

    public static SqliteTestDb CreateCentro(out CentroMaritimoDbContext db)
    {
        var fixture = new SqliteTestDb();
        var options = new DbContextOptionsBuilder<CentroMaritimoDbContext>()
            .UseSqlite(fixture._connection).Options;
        db = new CentroMaritimoDbContext(options);
        db.Database.EnsureCreated();
        return fixture;
    }

    public void Dispose() => _connection.Dispose();
}
