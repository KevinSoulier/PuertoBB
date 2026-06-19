using PuertoBB.Core.Entities.CamaraPortuaria;

namespace PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;

public interface IEmpresaRepository : IRepository<Empresa>
{
    /// <summary>Empresa con sus emails y grupos cargados.</summary>
    Task<Empresa?> GetConDetalleAsync(int id, CancellationToken ct = default);

    /// <summary>Todas las empresas con sus emails (para listados/emisión).</summary>
    Task<IReadOnlyList<Empresa>> GetTodasConEmailsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Empresa>> GetActivasAsync(CancellationToken ct = default);
}
