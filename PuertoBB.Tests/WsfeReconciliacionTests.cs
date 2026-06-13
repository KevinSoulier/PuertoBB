using Afip;
using Afip.Abstractions;
using Afip.Wsaa;
using Afip.Wsfe;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace PuertoBB.Tests;

/// <summary>
/// P2-2: si la respuesta de FECAESolicitar se pierde después de que AFIP autorizó el comprobante,
/// WsfeService debe reconciliar vía FECompConsultar en lugar de fallar (y nunca adoptar un
/// comprobante que no coincida con lo pedido).
/// </summary>
public class WsfeReconciliacionTests
{
    private static readonly AfipOptions Options = new() { Cuit = "30700000007", UsarHomologacion = true };

    private static AfipComprobanteRequest Request(decimal importe = 1500m) => new()
    {
        CodigoComprobante = 15,
        PuntoDeVenta = 1,
        DocNroReceptor = 30700000001,
        CondicionIvaReceptorId = 1,
        ImporteTotal = importe,
        FechaComprobante = new DateTime(2026, 6, 11),
        ServicioDesde = 20260601,
        ServicioHasta = 20260630,
        VencimientoPago = new DateTime(2026, 7, 11)
    };

    private static (WsfeService service, IWsfeClient wsfe) Build(long ultimoAutorizado)
    {
        var ticket = Substitute.For<ITicketProvider>();
        ticket.GetTicketAsync(Arg.Any<string>(), Arg.Any<AfipOptions>(), Arg.Any<CancellationToken>())
            .Returns(new AfipTicket("tok", "sig", DateTime.Now.AddHours(12)));
        var wsfe = Substitute.For<IWsfeClient>();
        wsfe.UltimoComprobanteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ultimoAutorizado);
        return (new WsfeService(ticket, wsfe), wsfe);
    }

    private static void SolicitarCaeLanzaTimeout(IWsfeClient wsfe)
        => wsfe.SolicitarCaeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<WsfeCaeRequest>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("se perdió la respuesta"));

    [Fact]
    public async Task SolicitarCae_TimeoutYConsultaCoincide_DevuelveCaeReconciliado()
    {
        var (service, wsfe) = Build(ultimoAutorizado: 7);     // va a intentar emitir el Nro 8
        SolicitarCaeLanzaTimeout(wsfe);
        wsfe.ConsultarComprobanteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                1, 15, 8, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new WsfeComprobanteConsultado
            {
                Numero = 8,
                ImporteTotal = 1500m,
                DocNro = 30700000001,
                FechaComprobante = new DateTime(2026, 6, 11),
                Cae = "CAE12345678",
                FechaVencimientoCae = new DateTime(2026, 6, 21)
            });

        var r = await service.SolicitarCaeAsync(Options, Request());

        Assert.True(r.Aprobado);
        Assert.Equal("CAE12345678", r.Cae);
        Assert.Equal(8, r.Numero);                            // mismo número: no se emitió dos veces
        Assert.Contains("Reconciliado", r.Observaciones);
    }

    [Fact]
    public async Task SolicitarCae_TimeoutYConsultaNoCoincide_PropagaError()
    {
        var (service, wsfe) = Build(ultimoAutorizado: 7);
        SolicitarCaeLanzaTimeout(wsfe);
        wsfe.ConsultarComprobanteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new WsfeComprobanteConsultado
            {
                Numero = 8,
                ImporteTotal = 999m,                          // importe distinto: ese comprobante NO es el nuestro
                DocNro = 30700000001,
                FechaComprobante = new DateTime(2026, 6, 11),
                Cae = "CAEAJENO"
            });

        // No se pudo confirmar → respuesta perdida; el error original viaja como InnerException.
        var ex = await Assert.ThrowsAsync<AfipRespuestaPerdidaException>(() => service.SolicitarCaeAsync(Options, Request()));
        Assert.IsType<TimeoutException>(ex.InnerException);
    }

    [Fact]
    public async Task SolicitarCae_TimeoutYConsultaFalla_PropagaErrorOriginal()
    {
        var (service, wsfe) = Build(ultimoAutorizado: 7);
        SolicitarCaeLanzaTimeout(wsfe);
        wsfe.ConsultarComprobanteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("la consulta también falló"));

        // Respuesta perdida; el InnerException debe ser el TimeoutException original, no el de la consulta.
        var ex = await Assert.ThrowsAsync<AfipRespuestaPerdidaException>(() => service.SolicitarCaeAsync(Options, Request()));
        Assert.IsType<TimeoutException>(ex.InnerException);
    }

    [Fact]
    public async Task SolicitarCae_TimeoutYComprobanteInexistente_PropagaError()
    {
        var (service, wsfe) = Build(ultimoAutorizado: 7);
        SolicitarCaeLanzaTimeout(wsfe);
        wsfe.ConsultarComprobanteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((WsfeComprobanteConsultado?)null);       // AFIP nunca recibió el comprobante

        var ex = await Assert.ThrowsAsync<AfipRespuestaPerdidaException>(() => service.SolicitarCaeAsync(Options, Request()));
        Assert.IsType<TimeoutException>(ex.InnerException);
    }

    [Fact]
    public async Task SolicitarCae_ImporteConMasDeDosDecimales_Reconcilia()
    {
        // A: el request trae 3 decimales; AFIP guarda el redondeado a 2. La comparación debe redondear
        // ambos lados (antes fallaba con == exacto → falso negativo → riesgo de duplicado).
        var (service, wsfe) = Build(ultimoAutorizado: 7);
        SolicitarCaeLanzaTimeout(wsfe);
        wsfe.ConsultarComprobanteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                1, 15, 8, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new WsfeComprobanteConsultado
            {
                Numero = 8,
                ImporteTotal = 1500.50m,                       // AFIP: redondeado a 2 decimales
                DocNro = 30700000001,
                FechaComprobante = new DateTime(2026, 6, 11),
                Cae = "CAEREDONDEO",
                FechaVencimientoCae = new DateTime(2026, 6, 21)
            });

        var r = await service.SolicitarCaeAsync(Options, Request(importe: 1500.504m));

        Assert.True(r.Aprobado);
        Assert.Equal("CAEREDONDEO", r.Cae);
        Assert.Equal(8, r.Numero);
    }

    [Fact]
    public async Task SolicitarCae_ConsultaFallaUnaVezLuegoResponde_Reconcilia()
    {
        // D: la consulta de reconciliación reintenta tras un fallo transitorio (misma red intermitente).
        var (service, wsfe) = Build(ultimoAutorizado: 7);
        SolicitarCaeLanzaTimeout(wsfe);
        var llamadas = 0;
        wsfe.ConsultarComprobanteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (++llamadas == 1) throw new TimeoutException("blip");
                return new WsfeComprobanteConsultado
                {
                    Numero = 8,
                    ImporteTotal = 1500m,
                    DocNro = 30700000001,
                    FechaComprobante = new DateTime(2026, 6, 11),
                    Cae = "CAEREINTENTO",
                    FechaVencimientoCae = new DateTime(2026, 6, 21)
                };
            });

        var r = await service.SolicitarCaeAsync(Options, Request());

        Assert.True(r.Aprobado);
        Assert.Equal("CAEREINTENTO", r.Cae);
        Assert.Equal(2, llamadas);                            // 1 fallo + 1 OK
    }

    [Fact]
    public async Task RecuperarSiYaEmitido_UltimoCoincide_DevuelveCae()
    {
        // B: recuperación ante respuesta perdida de un intento previo (crash/cancelación).
        var (service, wsfe) = Build(ultimoAutorizado: 8);     // AFIP ya tiene el Nro 8 autorizado
        wsfe.ConsultarComprobanteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                1, 15, 8, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new WsfeComprobanteConsultado
            {
                Numero = 8,
                ImporteTotal = 1500m,
                DocNro = 30700000001,
                FechaComprobante = new DateTime(2026, 6, 11),
                Cae = "CAERECUP",
                FechaVencimientoCae = new DateTime(2026, 6, 21)
            });

        var r = await service.RecuperarSiYaEmitidoAsync(Options, Request());

        Assert.NotNull(r);
        Assert.Equal(8, r!.Numero);
        Assert.Equal("CAERECUP", r.Cae);
    }

    [Fact]
    public async Task RecuperarSiYaEmitido_UltimoNoCoincide_DevuelveNull()
    {
        var (service, wsfe) = Build(ultimoAutorizado: 8);
        wsfe.ConsultarComprobanteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                1, 15, 8, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new WsfeComprobanteConsultado
            {
                Numero = 8,
                ImporteTotal = 999m,                           // el último de AFIP es de OTRO comprobante
                DocNro = 30700000001,
                FechaComprobante = new DateTime(2026, 6, 11),
                Cae = "CAEAJENO"
            });

        Assert.Null(await service.RecuperarSiYaEmitidoAsync(Options, Request()));
    }

    [Fact]
    public async Task RecuperarSiYaEmitido_SinComprobantesEnAfip_NoConsultaYDevuelveNull()
    {
        var (service, wsfe) = Build(ultimoAutorizado: 0);     // AFIP no tiene ningún comprobante

        Assert.Null(await service.RecuperarSiYaEmitidoAsync(Options, Request()));
        await wsfe.DidNotReceive().ConsultarComprobanteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }
}
