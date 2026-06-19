using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Infrastructure.Data;

namespace PuertoBB.Infrastructure.Repositories.CentroMaritimo;

public class AgenciaRepository : RepositoryBase<Agencia>, IAgenciaRepository
{
    private readonly CentroMaritimoDbContext _db;

    public AgenciaRepository(CentroMaritimoDbContext db, ILogger<AgenciaRepository> logger) : base(db, logger)
        => _db = db;

    public Task<Agencia?> GetConDetalleAsync(int id, CancellationToken ct = default)
        => _db.Agencias
            .Include(a => a.Emails)
            .Include(a => a.Grupos).ThenInclude(g => g.Grupo)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Agencia>> GetTodasConEmailsAsync(CancellationToken ct = default)
        => await _db.Agencias.AsNoTracking().Include(a => a.Emails).OrderBy(a => a.Nombre).ToListAsync(ct);

    public async Task<IReadOnlyList<Agencia>> GetActivasAsync(CancellationToken ct = default)
        => await _db.Agencias.AsNoTracking().Where(a => a.Activa).OrderBy(a => a.Nombre).ToListAsync(ct);
}
