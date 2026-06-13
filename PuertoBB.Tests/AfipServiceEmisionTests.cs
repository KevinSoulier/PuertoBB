using Afip;
using Afip.Wsfe;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PuertoBB.Core.Enums;
using PuertoBB.Core.Models.Afip;
using PuertoBB.Services.Afip;
using Xunit;

namespace PuertoBB.Tests;

/// <summary>
/// Pre-validaciones y traducción de errores de <see cref="AfipService.ObtenerCAEAsync"/>:
/// importe &gt; 0, fecha dentro de la ventana de AFIP (±10 días) y hints de rechazo.
/// </summary>
public class AfipServiceEmisionTests
{
    private static ComprobanteAfipRequest ValidReq() => new()
    {
        TipoComprobante = TipoComprobante.Recibo,
        CodigoAfip = 15,
        PuntoDeVenta = 1,
        CuitReceptor = "30000000007",
        CondicionIvaReceptorId = 1,
        ImporteTotal = 1000m,
        FechaEmision = DateTime.Today,
        PeriodoServicioDesde = 20260601,
        PeriodoServicioHasta = 20260630,
        FechaVencimientoPago = DateTime.Today.AddDays(10)
    };

    private static (AfipService svc, IWsfeService wsfe) Build()
    {
        var wsfe = Substitute.For<IWsfeService>();
        var svc = new AfipService(wsfe, new FakeAfipConfigProvider(conCertificado: true), NullLogger<AfipService>.Instance);
        return (svc, wsfe);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task ObtenerCAE_ImporteNoPositivo_FallaSinLlamarAfip(decimal importe)
    {
        var (svc, wsfe) = Build();

        var res = await svc.ObtenerCAEAsync(ValidReq() with { ImporteTotal = importe });

        Assert.False(res.Success);
        Assert.Contains("mayor a cero", res.ErrorMessage);
        await wsfe.DidNotReceive().SolicitarCaeAsync(Arg.Any<AfipOptions>(), Arg.Any<AfipComprobanteRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(15)]   // muy en el futuro
    [InlineData(-15)]  // muy en el pasado
    public async Task ObtenerCAE_FechaFueraDeVentana_FallaSinLlamarAfip(int dias)
    {
        var (svc, wsfe) = Build();

        var res = await svc.ObtenerCAEAsync(ValidReq() with { FechaEmision = DateTime.Today.AddDays(dias) });

        Assert.False(res.Success);
        Assert.Contains("días corridos", res.ErrorMessage);
        await wsfe.DidNotReceive().SolicitarCaeAsync(Arg.Any<AfipOptions>(), Arg.Any<AfipComprobanteRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ObtenerCAE_FechaEnElBorde_NoEsRechazadaPorLaValidacion()
    {
        var (svc, wsfe) = Build();
        wsfe.SolicitarCaeAsync(Arg.Any<AfipOptions>(), Arg.Any<AfipComprobanteRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AfipCaeResult { Aprobado = true, Cae = "75123456789012", Numero = 1, FechaVencimientoCae = DateTime.Today.AddDays(10) });

        var res = await svc.ObtenerCAEAsync(ValidReq() with { FechaEmision = DateTime.Today.AddDays(10) });

        Assert.True(res.Success);   // ±10 días es válido para AFIP (Concepto Servicios)
        await wsfe.Received().SolicitarCaeAsync(Arg.Any<AfipOptions>(), Arg.Any<AfipComprobanteRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("[10044] El campo ImpOpEx para comprobantes tipo C debe ser 0", "importe exento")]
    [InlineData("[10048] ImpTotal debe ser igual a ImpNeto + ImpTrib", "ImpTotal ≠ ImpNeto")]
    public async Task ObtenerCAE_Rechazo_TraduceElError(string observaciones, string hintEsperado)
    {
        var (svc, wsfe) = Build();
        wsfe.SolicitarCaeAsync(Arg.Any<AfipOptions>(), Arg.Any<AfipComprobanteRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AfipCaeResult { Aprobado = false, Observaciones = observaciones });

        var res = await svc.ObtenerCAEAsync(ValidReq());

        Assert.False(res.Success);
        Assert.Contains(hintEsperado, res.ErrorMessage);
    }
}
