using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Infrastructure.Data;

namespace PuertoBB.Infrastructure.Repositories.CentroMaritimo;

public class ClienteRepository : RepositoryBase<Cliente>, IClienteRepository
{
    private readonly CentroMaritimoDbContext _db;

    public ClienteRepository(CentroMaritimoDbContext db, ILogger<ClienteRepository> logger) : base(db, logger)
        => _db = db;

    public Task<Cliente?> GetConDetalleAsync(int id, CancellationToken ct = default)
        => _db.Clientes
            .Include(a => a.Emails)
            .Include(a => a.Grupos).ThenInclude(g => g.Grupo)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Cliente>> GetTodasConEmailsAsync(CancellationToken ct = default)
        => await _db.Clientes.AsNoTracking().Include(a => a.Emails).OrderBy(a => a.Nombre).ToListAsync(ct);

    public async Task<IReadOnlyList<Cliente>> GetActivasAsync(CancellationToken ct = default)
        => await _db.Clientes.AsNoTracking().Where(a => a.Activa).OrderBy(a => a.Nombre).ToListAsync(ct);
}
