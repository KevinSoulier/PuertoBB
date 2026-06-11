using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Infrastructure.Data;

namespace PuertoBB.Infrastructure.Repositories.CentroMaritimo;

public class GrupoFacturacionRepository : RepositoryBase<GrupoFacturacion>, IGrupoFacturacionRepository
{
    private readonly CentroMaritimoDbContext _db;

    public GrupoFacturacionRepository(CentroMaritimoDbContext db, ILogger<GrupoFacturacionRepository> logger) : base(db, logger)
        => _db = db;

    public Task<GrupoFacturacion?> GetConMiembrosAsync(int id, CancellationToken ct = default)
        => _db.Grupos
            .Include(g => g.Lineas)
            .Include(g => g.Agencias).ThenInclude(ag => ag.Agencia).ThenInclude(a => a.Emails)
            .FirstOrDefaultAsync(g => g.Id == id, ct);

    public async Task<IReadOnlyList<GrupoFacturacion>> GetActivosAsync(CancellationToken ct = default)
        => await _db.Grupos.AsNoTracking().Where(g => g.Activo).OrderBy(g => g.Nombre).ToListAsync(ct);
}
