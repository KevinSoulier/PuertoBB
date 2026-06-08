using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Afip;
using PuertoBB.Services.Security;

namespace CentroMaritimo.UI.Services;

/// <summary>
/// Provee la config AFIP del Centro Marítimo. Si se factura como apoderado, el CUIT emisor
/// (titular del certificado) es el del apoderado. La contraseña se descifra al leerla.
/// </summary>
public class AfipConfigProvider : IAfipConfigProvider
{
    private readonly IConfiguracionRepository _config;
    private readonly ISecretProtector _protector;

    public AfipConfigProvider(IConfiguracionRepository config, ISecretProtector protector)
    {
        _config = config;
        _protector = protector;
    }

    public async Task<AfipConfig> GetAsync(CancellationToken ct = default)
    {
        var c = await _config.GetAsync(ct);
        var pv = c.PuntoDeVentaActivo;
        var cuit = c.UsarApoderado && !string.IsNullOrWhiteSpace(c.CuitApoderado) ? c.CuitApoderado : c.Cuit;
        return new AfipConfig
        {
            CuitEmisor = new string((cuit ?? "").Where(char.IsDigit).ToArray()),
            CertificadoRuta = pv?.CertificadoRuta,
            CertificadoPassword = _protector.Unprotect(pv?.CertificadoPassword),
            CertificadoKeyRuta = pv?.CertificadoKeyRuta,
            UsarHomologacion = pv?.UsarHomologacion ?? false
        };
    }
}
