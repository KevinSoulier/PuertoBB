using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;

public interface IClienteRepository : IRepository<Cliente>
{
    Task<Cliente?> GetConDetalleAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Cliente>> GetTodasConEmailsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Cliente>> GetActivasAsync(CancellationToken ct = default);
}
