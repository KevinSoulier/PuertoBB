using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;

/// <summary>Acceso al singleton Configuracion (Id = 1) del Centro Marítimo.</summary>
public interface IConfiguracionRepository
{
    Task<Configuracion> GetAsync(CancellationToken ct = default);
    Task SaveAsync(Configuracion configuracion, CancellationToken ct = default);
}
