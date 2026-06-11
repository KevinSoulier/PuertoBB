using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;

public interface IAgenciaRepository : IRepository<Agencia>
{
    Task<Agencia?> GetConDetalleAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Agencia>> GetTodasConEmailsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Agencia>> GetActivasAsync(CancellationToken ct = default);

    Task SetMorosoAsync(int id, bool esMoroso, CancellationToken ct = default);
}
