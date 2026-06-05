using PuertoBB.Core.Entities.CamaraPortuaria;

namespace PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;

public interface IGrupoFacturacionRepository : IRepository<GrupoFacturacion>
{
    /// <summary>Grupo con sus empresas (y emails de cada una) cargados.</summary>
    Task<GrupoFacturacion?> GetConMiembrosAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<GrupoFacturacion>> GetActivosAsync(CancellationToken ct = default);
}
