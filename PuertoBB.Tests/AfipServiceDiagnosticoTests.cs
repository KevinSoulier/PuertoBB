using Afip;
using Afip.Abstractions;
using Afip.Wsfe;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PuertoBB.Services.Afip;
using Xunit;

namespace PuertoBB.Tests;

/// <summary>
/// Diagnóstico extendido de "Probar conexión": verificaciones FEParamGet* (punto de venta,
/// tipo de comprobante y condiciones IVA del receptor) tras autenticación OK.
/// </summary>
public class AfipServiceDiagnosticoTests
{
    private static IWsfeService WsfeOk(
        IReadOnlyList<WsfePuntoVenta>? pvs = null,
        IReadOnlyList<WsfeTipoComprobante>? tipos = null,
        IReadOnlyList<WsfeCondicionIvaReceptor>? condiciones = null)
    {
        var wsfe = Substitute.For<IWsfeService>();
        wsfe.VerificarServicioAsync(Arg.Any<AfipOptions>(), Arg.Any<CancellationToken>()).Returns(true);
        wsfe.UltimoComprobanteAsync(Arg.Any<AfipOptions>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(7L);
        wsfe.ObtenerPuntosVentaAsync(Arg.Any<AfipOptions>(), Arg.Any<CancellationToken>())
            .Returns(pvs ?? [new WsfePuntoVenta { Numero = 1, EmisionTipo = "CAE" }]);
        wsfe.ObtenerTiposComprobanteAsync(Arg.Any<AfipOptions>(), Arg.Any<CancellationToken>())
            .Returns(tipos ?? [new WsfeTipoComprobante { Id = 15, Descripcion = "Recibo C" }]);
        wsfe.ObtenerCondicionesIvaReceptorAsync(Arg.Any<AfipOptions>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(condiciones ?? [new WsfeCondicionIvaReceptor { Id = 1, Descripcion = "IVA Responsable Inscripto" }]);
        return wsfe;
    }

    private static AfipService Build(IWsfeService wsfe)
        => new(wsfe, new FakeAfipConfigProvider(conCertificado: true), NullLogger<AfipService>.Instance);

    [Fact]
    public async Task ProbarConexion_TodoOk_ValidaPvTipoYCondiciones()
    {
        var res = await Build(WsfeOk()).ProbarConexionAsync(puntoVenta: 1, codigoComprobante: 15);

        var d = res.Data!;
        Assert.True(d.AutenticacionOk);
        Assert.True(d.PuntoVentaOk);
        Assert.True(d.TipoComprobanteOk);
        Assert.NotNull(d.CondicionesIvaReceptor);
        Assert.Contains("1 — IVA Responsable Inscripto", d.CondicionesIvaReceptor!);
    }

    [Fact]
    public async Task ProbarConexion_PvNoHabilitado_MarcaPuntoVentaFalse()
    {
        var wsfe = WsfeOk(pvs: [new WsfePuntoVenta { Numero = 99, EmisionTipo = "CAE" }]);

        var d = (await Build(wsfe).ProbarConexionAsync(1, 15)).Data!;

        Assert.True(d.AutenticacionOk);
        Assert.False(d.PuntoVentaOk);
        Assert.Contains("NO figura", d.Detalle);
    }

    [Fact]
    public async Task ProbarConexion_PvBloqueado_OCaea_MarcaPuntoVentaFalse()
    {
        var bloqueado = WsfeOk(pvs: [new WsfePuntoVenta { Numero = 1, EmisionTipo = "CAE", Bloqueado = true }]);
        Assert.False((await Build(bloqueado).ProbarConexionAsync(1, 15)).Data!.PuntoVentaOk);

        var caea = WsfeOk(pvs: [new WsfePuntoVenta { Numero = 1, EmisionTipo = "CAEA" }]);
        Assert.False((await Build(caea).ProbarConexionAsync(1, 15)).Data!.PuntoVentaOk);
    }

    [Fact]
    public async Task ProbarConexion_ListaPvVacia_DejaNullSinFallar()
    {
        var wsfe = WsfeOk(pvs: []);

        var d = (await Build(wsfe).ProbarConexionAsync(1, 15)).Data!;

        Assert.True(d.AutenticacionOk);
        Assert.Null(d.PuntoVentaOk);   // no verificable ≠ mal
        Assert.Contains("habitual en homologación", d.Detalle);
    }

    [Fact]
    public async Task ProbarConexion_TipoNoVigente_MarcaTipoFalse()
    {
        var wsfe = WsfeOk(tipos: [new WsfeTipoComprobante { Id = 15, Descripcion = "Recibo C", VigenteHasta = new DateTime(2020, 1, 1) }]);

        var d = (await Build(wsfe).ProbarConexionAsync(1, 15)).Data!;

        Assert.False(d.TipoComprobanteOk);
        Assert.Contains("fuera de vigencia", d.Detalle);
    }

    [Fact]
    public async Task ProbarConexion_FEParamGetFalla_NoDegradaAutenticacion()
    {
        var wsfe = WsfeOk();
        wsfe.ObtenerPuntosVentaAsync(Arg.Any<AfipOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var d = (await Build(wsfe).ProbarConexionAsync(1, 15)).Data!;

        Assert.True(d.AutenticacionOk);
        Assert.Null(d.PuntoVentaOk);
        Assert.Contains("no se pudo verificar", d.Detalle);
        Assert.True(d.TipoComprobanteOk);   // las demás verificaciones siguen corriendo
    }
}
