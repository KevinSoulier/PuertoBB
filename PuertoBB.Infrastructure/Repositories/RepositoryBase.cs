using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Entities.Common;
using PuertoBB.Core.Exceptions;
using PuertoBB.Core.Interfaces.Repositories;

namespace PuertoBB.Infrastructure.Repositories;

/// <summary>
/// Repositorio genérico CRUD. Las operaciones de escritura persisten inmediatamente
/// (la app es unipersonal, sin Unit of Work explícito) y envuelven DbUpdateException
/// en ReciboException con mensaje legible, según convenciones.md.
/// </summary>
public abstract class RepositoryBase<T> : IRepository<T> where T : BaseEntity
{
    protected readonly DbContext Db;
    protected readonly ILogger Logger;
    protected DbSet<T> Set => Db.Set<T>();

    protected RepositoryBase(DbContext db, ILogger logger)
    {
        Db = db;
        Logger = logger;
    }

    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
        => await Set.FindAsync([id], ct);

    public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default)
        => await Set.AsNoTracking().ToListAsync(ct);

    public virtual async Task AddAsync(T entity, CancellationToken ct = default)
    {
        // CreatedAt/UpdatedAt los sella PuertoBBDbContext en SaveChanges (única fuente de verdad).
        await Set.AddAsync(entity, ct);
        await GuardarAsync(ct);
    }

    public virtual async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        Set.Update(entity);
        await GuardarAsync(ct);
    }

    public virtual async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await Set.FindAsync([id], ct);
        if (entity is null) return;
        Set.Remove(entity);
        await GuardarAsync(ct);
    }

    /// <summary>Persiste y traduce errores de DB a ReciboException.</summary>
    protected async Task GuardarAsync(CancellationToken ct)
    {
        try
        {
            await Db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            Logger.LogError(ex, "Error al persistir {Cliente}", typeof(T).Name);
            throw new ReciboException(
                $"No se pudo guardar {typeof(T).Name}. Verifique que no haya duplicados o datos inválidos.", ex);
        }
    }
}
