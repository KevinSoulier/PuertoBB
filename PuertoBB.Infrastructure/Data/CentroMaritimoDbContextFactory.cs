using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PuertoBB.Infrastructure.Data;

/// <summary>
/// Factory de diseño usada por `dotnet ef migrations` / `dotnet ef database update`.
/// No se usa en runtime (la app configura el contexto vía DI).
/// </summary>
public class CentroMaritimoDbContextFactory : IDesignTimeDbContextFactory<CentroMaritimoDbContext>
{
    public CentroMaritimoDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CentroMaritimoDbContext>()
            .UseSqlite("Data Source=centro-maritimo.db")
            .Options;
        return new CentroMaritimoDbContext(options);
    }
}
