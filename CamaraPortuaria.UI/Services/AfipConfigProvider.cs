using PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Afip;
using PuertoBB.Services.Security;

namespace CamaraPortuaria.UI.Services;

/// <summary>Provee la config AFIP leyendo el singleton Configuracion de la Cámara Portuaria. La contraseña se descifra al leerla.</summary>
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
        return new AfipConfig
        {
            CuitEmisor = new string((c.Cuit ?? "").Where(char.IsDigit).ToArray()),
            CertificadoRuta = pv?.CertificadoRuta,
            CertificadoPassword = _protector.Unprotect(pv?.CertificadoPassword),
            CertificadoKeyRuta = pv?.CertificadoKeyRuta,
            UsarHomologacion = pv?.UsarHomologacion ?? false
        };
    }
}
