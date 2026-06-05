using Microsoft.EntityFrameworkCore;

namespace PuertoBB.Infrastructure.Data;

public class CamaraPortuariaDbContext : DbContext
{
    public CamaraPortuariaDbContext(DbContextOptions<CamaraPortuariaDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CamaraPortuariaDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
