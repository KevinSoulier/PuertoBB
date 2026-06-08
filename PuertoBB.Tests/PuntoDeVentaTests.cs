using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Infrastructure.Repositories.CamaraPortuaria;
using PuertoBB.Tests.TestSupport;
using Xunit;

namespace PuertoBB.Tests;

public class PuntoDeVentaTests
{
    [Fact]
    public async Task Seed_TieneUnPuntoDeVentaActivoPorDefecto()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var repo = new ConfiguracionRepository(db);

        var puntos = await repo.GetPuntosDeVentaAsync();
        var activo = puntos.Single(p => p.Activo);
        Assert.Equal("Principal", activo.Nombre);

        var config = await repo.GetAsync();
        Assert.NotNull(config.PuntoDeVentaActivo);
        Assert.Equal(activo.Id, config.PuntoDeVentaActivo!.Id);
    }

    [Fact]
    public async Task MarcarActivo_DejaSoloUnoActivo_YQuedaEnConfig()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var repo = new ConfiguracionRepository(db);

        var homo = await repo.GuardarPuntoDeVentaAsync(new PuntoDeVenta
        {
            Nombre = "Homologación",
            Numero = 5,
            UsarHomologacion = true
        });

        await repo.MarcarPuntoDeVentaActivoAsync(homo.Id);

        var puntos = await repo.GetPuntosDeVentaAsync();
        var activo = puntos.Single(p => p.Activo);   // exactamente uno activo
        Assert.Equal("Homologación", activo.Nombre);
        Assert.True(activo.UsarHomologacion);

        var config = await repo.GetAsync();
        Assert.Equal(5, config.PuntoDeVentaActivo!.Numero);
        Assert.True(config.PuntoDeVentaActivo.UsarHomologacion);
    }

    [Fact]
    public async Task Eliminar_QuitaElPuntoDeVenta()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var repo = new ConfiguracionRepository(db);

        var nuevo = await repo.GuardarPuntoDeVentaAsync(new PuntoDeVenta { Nombre = "Temporal", Numero = 9 });
        Assert.Equal(2, (await repo.GetPuntosDeVentaAsync()).Count);

        await repo.EliminarPuntoDeVentaAsync(nuevo.Id);
        var restantes = await repo.GetPuntosDeVentaAsync();
        Assert.Single(restantes);
        Assert.DoesNotContain(restantes, p => p.Id == nuevo.Id);
    }
}
