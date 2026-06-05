using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;

public interface IBarcoRepository : IRepository<Barco>
{
    Task<Barco?> GetPorNombreAsync(string nombre, CancellationToken ct = default);
}
