using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;

public interface IGrupoFacturacionRepository : IRepository<GrupoFacturacion>
{
    Task<GrupoFacturacion?> GetConMiembrosAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<GrupoFacturacion>> GetActivosAsync(CancellationToken ct = default);
}
