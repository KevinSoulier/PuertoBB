using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Afip;

namespace PuertoBB.Tests;

internal class FakeAfipConfigProvider : IAfipConfigProvider
{
    private readonly AfipConfig _config;

    public FakeAfipConfigProvider(string cuitEmisor = "30000000007")
        => _config = new AfipConfig { CuitEmisor = cuitEmisor, UsarHomologacion = true };

    public Task<AfipConfig> GetAsync(CancellationToken ct = default)
        => Task.FromResult(_config);
}
