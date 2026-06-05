using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;
using PuertoBB.Infrastructure.Data;

namespace PuertoBB.Infrastructure.Repositories.CamaraPortuaria;

public class GrupoFacturacionRepository : RepositoryBase<GrupoFacturacion>, IGrupoFacturacionRepository
{
    private readonly CamaraPortuariaDbContext _db;

    public GrupoFacturacionRepository(CamaraPortuariaDbContext db, ILogger<GrupoFacturacionRepository> logger) : base(db, logger)
        => _db = db;

    public Task<GrupoFacturacion?> GetConMiembrosAsync(int id, CancellationToken ct = default)
        => _db.Grupos
            .Include(g => g.Empresas).ThenInclude(eg => eg.Empresa).ThenInclude(e => e.Emails)
            .FirstOrDefaultAsync(g => g.Id == id, ct);

    public async Task<IReadOnlyList<GrupoFacturacion>> GetActivosAsync(CancellationToken ct = default)
        => await _db.Grupos.AsNoTracking().Where(g => g.Activo).OrderBy(g => g.Nombre).ToListAsync(ct);
}
