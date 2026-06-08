using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Infrastructure.Data;

namespace PuertoBB.Infrastructure.Repositories.CentroMaritimo;

public class ConceptoReciboRepository : RepositoryBase<ConceptoRecibo>, IConceptoReciboRepository
{
    private readonly CentroMaritimoDbContext _db;

    public ConceptoReciboRepository(CentroMaritimoDbContext db, ILogger<ConceptoReciboRepository> logger) : base(db, logger)
        => _db = db;

    public Task<ConceptoRecibo?> GetPorNombreAsync(string nombre, CancellationToken ct = default)
        => _db.ConceptosRecibo.FirstOrDefaultAsync(x => x.Nombre == nombre, ct);
}
