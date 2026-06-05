using PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Afip;

namespace CamaraPortuaria.UI.Services;

/// <summary>Provee la config AFIP leyendo el singleton Configuracion de la Cámara Portuaria.</summary>
public class AfipConfigProvider : IAfipConfigProvider
{
    private readonly IConfiguracionRepository _config;
    public AfipConfigProvider(IConfiguracionRepository config) => _config = config;

    public async Task<AfipConfig> GetAsync(CancellationToken ct = default)
    {
        var c = await _config.GetAsync(ct);
        return new AfipConfig
        {
            CuitEmisor = new string((c.Cuit ?? "").Where(char.IsDigit).ToArray()),
            CertificadoRuta = c.AfipCertificadoRuta,
            CertificadoPassword = c.AfipCertificadoPassword,
            UsarHomologacion = c.AfipUsarHomologacion
        };
    }
}
