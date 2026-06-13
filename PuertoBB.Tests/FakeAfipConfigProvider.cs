using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Afip;

namespace PuertoBB.Tests;

internal class FakeAfipConfigProvider : IAfipConfigProvider
{
    private readonly AfipConfig _config;

    /// <param name="conCertificado">true = simula certificado cargado (habilita el camino de autenticación).</param>
    public FakeAfipConfigProvider(string cuitEmisor = "30000000007", bool conCertificado = false)
        => _config = new AfipConfig
        {
            CuitEmisor = cuitEmisor,
            UsarHomologacion = true,
            CertificadoContenido = conCertificado ? [1, 2, 3] : null
        };

    public Task<AfipConfig> GetAsync(CancellationToken ct = default)
        => Task.FromResult(_config);
}
