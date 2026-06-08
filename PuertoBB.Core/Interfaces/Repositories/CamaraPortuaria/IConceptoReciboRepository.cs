using PuertoBB.Core.Entities.CamaraPortuaria;

namespace PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;

public interface IConceptoReciboRepository : IRepository<ConceptoRecibo>
{
    Task<ConceptoRecibo?> GetPorNombreAsync(string nombre, CancellationToken ct = default);
}
