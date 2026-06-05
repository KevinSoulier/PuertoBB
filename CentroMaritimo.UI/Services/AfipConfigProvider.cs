using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Afip;

namespace CentroMaritimo.UI.Services;

/// <summary>
/// Provee la config AFIP del Centro Marítimo. Si se factura como apoderado, el CUIT emisor
/// (titular del certificado) es el del apoderado.
/// </summary>
public class AfipConfigProvider : IAfipConfigProvider
{
    private readonly IConfiguracionRepository _config;
    public AfipConfigProvider(IConfiguracionRepository config) => _config = config;

    public async Task<AfipConfig> GetAsync(CancellationToken ct = default)
    {
        var c = await _config.GetAsync(ct);
        var cuit = c.UsarApoderado && !string.IsNullOrWhiteSpace(c.CuitApoderado) ? c.CuitApoderado : c.Cuit;
        return new AfipConfig
        {
            CuitEmisor = new string((cuit ?? "").Where(char.IsDigit).ToArray()),
            CertificadoRuta = c.AfipCertificadoRuta,
            CertificadoPassword = c.AfipCertificadoPassword,
            UsarHomologacion = c.AfipUsarHomologacion
        };
    }
}
