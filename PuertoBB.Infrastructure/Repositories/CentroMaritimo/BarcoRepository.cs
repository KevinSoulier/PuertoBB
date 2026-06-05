using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Infrastructure.Data;

namespace PuertoBB.Infrastructure.Repositories.CentroMaritimo;

public class BarcoRepository : RepositoryBase<Barco>, IBarcoRepository
{
    private readonly CentroMaritimoDbContext _db;

    public BarcoRepository(CentroMaritimoDbContext db, ILogger<BarcoRepository> logger) : base(db, logger)
        => _db = db;

    public Task<Barco?> GetPorNombreAsync(string nombre, CancellationToken ct = default)
        => _db.Barcos.FirstOrDefaultAsync(x => x.Nombre == nombre, ct);
}
