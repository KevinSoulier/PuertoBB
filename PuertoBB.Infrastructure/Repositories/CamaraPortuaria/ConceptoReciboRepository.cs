using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;
using PuertoBB.Infrastructure.Data;

namespace PuertoBB.Infrastructure.Repositories.CamaraPortuaria;

public class ConceptoReciboRepository : RepositoryBase<ConceptoRecibo>, IConceptoReciboRepository
{
    private readonly CamaraPortuariaDbContext _db;

    public ConceptoReciboRepository(CamaraPortuariaDbContext db, ILogger<ConceptoReciboRepository> logger) : base(db, logger)
        => _db = db;

    public Task<ConceptoRecibo?> GetPorNombreAsync(string nombre, CancellationToken ct = default)
        => _db.ConceptosRecibo.FirstOrDefaultAsync(x => x.Nombre == nombre, ct);
}
