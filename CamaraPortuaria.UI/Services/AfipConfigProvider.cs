using PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Afip;

namespace CamaraPortuaria.UI.Services;

/// <summary>Provee la config AFIP leyendo el singleton Configuracion de la Cámara Portuaria.</summary>
public class AfipConfigProvider : IAfipConfigProvider
{
    private readonly IConfiguracionRepository _config;

    public AfipConfigProvider(IConfiguracionRepository config)
    {
        _config = config;
    }

    public async Task<AfipConfig> GetAsync(CancellationToken ct = default)
    {
        // Lectura sin tracking: refleja siempre el PV activo actual aunque este provider quede capturado
        // por un DbContext de larga vida (si no, "Probar conexión" usaría el certificado del PV anterior).
        var c = await _config.GetSinTrackingAsync(ct);
        var pv = c.PuntoDeVentaActivo;
        return new AfipConfig
        {
            CuitEmisor = new string((c.Cuit ?? "").Where(char.IsDigit).ToArray()),
            RazonSocial = c.RazonSocial,
            CertificadoContenido = pv?.CertificadoContenido,
            CertificadoPassword = pv?.CertificadoPassword,
            CertificadoKeyContenido = pv?.CertificadoKeyContenido,
            UsarHomologacion = pv?.UsarHomologacion ?? false,
            IngresosBrutos = c.IngresosBrutos,
            InicioActividades = c.InicioActividades
        };
    }
}
