using PuertoBB.Core.Models.Afip;

namespace PuertoBB.Core.Interfaces.Services;

/// <summary>
/// Provee la configuración AFIP vigente. Cada app la implementa leyendo su Configuracion singleton,
/// desacoplando el servicio AFIP (en Services) del modelo de datos concreto de cada app.
/// </summary>
public interface IAfipConfigProvider
{
    Task<AfipConfig> GetAsync(CancellationToken ct = default);
}
