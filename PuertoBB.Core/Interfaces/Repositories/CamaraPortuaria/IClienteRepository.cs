using PuertoBB.Core.Entities.CamaraPortuaria;

namespace PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;

public interface IClienteRepository : IRepository<Cliente>
{
    /// <summary>Cliente con sus emails y grupos cargados.</summary>
    Task<Cliente?> GetConDetalleAsync(int id, CancellationToken ct = default);

    /// <summary>Todas las empresas con sus emails (para listados/emisión).</summary>
    Task<IReadOnlyList<Cliente>> GetTodasConEmailsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Cliente>> GetActivasAsync(CancellationToken ct = default);
}
