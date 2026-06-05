using PuertoBB.Core.Entities.CamaraPortuaria;

namespace PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;

/// <summary>Acceso al singleton Configuracion (Id = 1).</summary>
public interface IConfiguracionRepository
{
    Task<Configuracion> GetAsync(CancellationToken ct = default);
    Task SaveAsync(Configuracion configuracion, CancellationToken ct = default);
}
