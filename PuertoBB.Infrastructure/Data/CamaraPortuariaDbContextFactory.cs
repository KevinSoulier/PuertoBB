using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PuertoBB.Infrastructure.Data;

/// <summary>
/// Factory de diseño usada por `dotnet ef migrations` / `dotnet ef database update`.
/// No se usa en runtime (la app configura el contexto vía DI).
/// </summary>
public class CamaraPortuariaDbContextFactory : IDesignTimeDbContextFactory<CamaraPortuariaDbContext>
{
    public CamaraPortuariaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CamaraPortuariaDbContext>()
            .UseSqlite("Data Source=camara-portuaria.db")
            .Options;
        return new CamaraPortuariaDbContext(options);
    }
}
