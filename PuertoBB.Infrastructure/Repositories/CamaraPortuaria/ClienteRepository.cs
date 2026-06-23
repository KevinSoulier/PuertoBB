using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;
using PuertoBB.Infrastructure.Data;

namespace PuertoBB.Infrastructure.Repositories.CamaraPortuaria;

public class ClienteRepository : RepositoryBase<Cliente>, IClienteRepository
{
    private readonly CamaraPortuariaDbContext _db;

    public ClienteRepository(CamaraPortuariaDbContext db, ILogger<ClienteRepository> logger) : base(db, logger)
        => _db = db;

    public Task<Cliente?> GetConDetalleAsync(int id, CancellationToken ct = default)
        => _db.Clientes
            .Include(e => e.Emails)
            .Include(e => e.Grupos).ThenInclude(g => g.Grupo)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<Cliente>> GetTodasConEmailsAsync(CancellationToken ct = default)
        => await _db.Clientes.AsNoTracking().Include(e => e.Emails).OrderBy(e => e.Nombre).ToListAsync(ct);

    public async Task<IReadOnlyList<Cliente>> GetActivasAsync(CancellationToken ct = default)
        => await _db.Clientes.AsNoTracking().Where(e => e.Activa).OrderBy(e => e.Nombre).ToListAsync(ct);
}
