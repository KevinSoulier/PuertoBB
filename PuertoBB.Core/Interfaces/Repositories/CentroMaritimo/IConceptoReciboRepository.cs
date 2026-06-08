using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;

public interface IConceptoReciboRepository : IRepository<ConceptoRecibo>
{
    Task<ConceptoRecibo?> GetPorNombreAsync(string nombre, CancellationToken ct = default);
}
