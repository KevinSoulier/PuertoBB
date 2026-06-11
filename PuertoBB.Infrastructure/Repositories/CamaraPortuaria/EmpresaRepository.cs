using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;
using PuertoBB.Infrastructure.Data;

namespace PuertoBB.Infrastructure.Repositories.CamaraPortuaria;

public class EmpresaRepository : RepositoryBase<Empresa>, IEmpresaRepository
{
    private readonly CamaraPortuariaDbContext _db;

    public EmpresaRepository(CamaraPortuariaDbContext db, ILogger<EmpresaRepository> logger) : base(db, logger)
        => _db = db;

    public Task<Empresa?> GetConDetalleAsync(int id, CancellationToken ct = default)
        => _db.Empresas
            .Include(e => e.Emails)
            .Include(e => e.Grupos).ThenInclude(g => g.Grupo)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<Empresa>> GetTodasConEmailsAsync(CancellationToken ct = default)
        => await _db.Empresas.AsNoTracking().Include(e => e.Emails).OrderBy(e => e.Nombre).ToListAsync(ct);

    public async Task<IReadOnlyList<Empresa>> GetActivasAsync(CancellationToken ct = default)
        => await _db.Empresas.AsNoTracking().Where(e => e.Activa).OrderBy(e => e.Nombre).ToListAsync(ct);

    public async Task SetMorosoAsync(int id, bool esMoroso, CancellationToken ct = default)
    {
        var e = await _db.Empresas.FindAsync([id], ct);
        if (e is null) return;
        e.EsMoroso = esMoroso;
        await _db.SaveChangesAsync(ct);
    }
}
