using Afip.Wsaa;
using Xunit;

namespace PuertoBB.Tests;

public class TicketCacheTests
{
    private static Func<Task<AfipTicket>> Renovador(Action onCall, DateTime expiracion, string prefijo = "t")
        => () => { onCall(); return Task.FromResult(new AfipTicket($"{prefijo}-token", $"{prefijo}-sign", expiracion)); };

    [Fact]
    public async Task GetOrRenew_ReusaTicketVigente_YRenuevaPorServicioDistinto()
    {
        var cache = new TicketCache(new InMemoryTicketStore());
        var futuro = DateTime.Now.AddHours(12);
        var llamadas = 0;

        var a = await cache.GetOrRenewAsync("20111111112", "wsfe", Renovador(() => llamadas++, futuro, "wsfe"));
        var b = await cache.GetOrRenewAsync("20111111112", "wsfe", Renovador(() => llamadas++, futuro, "wsfe"));

        Assert.Equal(1, llamadas);          // el segundo pedido reusó el cacheado
        Assert.Equal(a, b);

        // Otro servicio (mismo CUIT) tiene su propio TA: debe renovar
        var c = await cache.GetOrRenewAsync("20111111112", "wsremcarne", Renovador(() => llamadas++, futuro, "rem"));
        Assert.Equal(2, llamadas);
        Assert.NotEqual(a.Token, c.Token);
    }

    [Fact]
    public async Task GetOrRenew_RenuevaCuandoElTicketExpiraDentroDelMargen()
    {
        var cache = new TicketCache(new InMemoryTicketStore());
        var llamadas = 0;
        // Expira en 5 min → dentro del margen de 10 min ⇒ se considera no vigente y siempre renueva.
        var casiExpirado = DateTime.Now.AddMinutes(5);

        await cache.GetOrRenewAsync("20", "wsfe", Renovador(() => llamadas++, casiExpirado));
        await cache.GetOrRenewAsync("20", "wsfe", Renovador(() => llamadas++, casiExpirado));

        Assert.Equal(2, llamadas);
    }
}

public class FileTicketStoreTests
{
    [Fact]
    public void GuardaYRecupera_RoundTrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "afip-ta-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileTicketStore(dir);
            var ticket = new AfipTicket("tok-123", "sig-456", DateTime.Now.AddHours(12));

            store.Save("20111111112:wsfe", ticket);
            var leido = store.Load("20111111112:wsfe");

            Assert.NotNull(leido);
            Assert.Equal("tok-123", leido!.Token);
            Assert.Equal("sig-456", leido.Sign);
            Assert.Null(store.Load("clave-inexistente"));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
