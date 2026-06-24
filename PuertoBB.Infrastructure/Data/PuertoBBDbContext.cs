using Microsoft.EntityFrameworkCore;
using PuertoBB.Core.Entities.Common;

namespace PuertoBB.Infrastructure.Data;

/// <summary>
/// Base común de los DbContext de la solución. Centraliza el sello de auditoría
/// (CreatedAt/UpdatedAt) sobre <see cref="BaseEntity"/> para que ningún guardado lo
/// omita, incluso los repositorios que invocan SaveChanges directo (Configuracion,
/// ContadorVoucher). Es la única fuente de verdad para los timestamps.
/// </summary>
public abstract class PuertoBBDbContext : DbContext
{
    protected PuertoBBDbContext(DbContextOptions options) : base(options) { }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        SellarAuditoria();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        SellarAuditoria();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Sella CreatedAt al insertar (solo si viene sin asignar, para respetar valores
    /// explícitos en importes/seeds) y UpdatedAt en cada modificación.
    /// </summary>
    private void SellarAuditoria()
    {
        var ahora = DateTime.Now;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAt == default)
                    entry.Entity.CreatedAt = ahora;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = ahora;
            }
        }
    }
}
