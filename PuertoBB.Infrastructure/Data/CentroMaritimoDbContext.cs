using Microsoft.EntityFrameworkCore;

namespace PuertoBB.Infrastructure.Data;

public class CentroMaritimoDbContext : DbContext
{
    public CentroMaritimoDbContext(DbContextOptions<CentroMaritimoDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CentroMaritimoDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
