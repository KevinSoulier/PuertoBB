using Afip;
using Afip.Abstractions;
using Afip.Mock;
using Afip.Padron;
using Afip.Wsaa;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PuertoBB.Services.Afip;
using Xunit;

namespace PuertoBB.Tests;

/// <summary>Adaptador de dominio para "Validar CUIT en ARCA" (constancia de inscripción).</summary>
public class AfipPadronServiceTests
{
    private static AfipPadronService Build(IPadronService? padron = null, bool conCertificado = true)
        => new(padron ?? Substitute.For<IPadronService>(),
               new FakeAfipConfigProvider(conCertificado: conCertificado),
               NullLogger<AfipPadronService>.Instance);

    [Fact]
    public async Task CuitInvalido_Falla_SinConsultarPadron()
    {
        var padron = Substitute.For<IPadronService>();

        var res = await Build(padron).ConsultarCuitAsync("30111111110");   // dígito verificador incorrecto

        Assert.False(res.Success);
        Assert.Contains("CUIT", res.ErrorMessage);
        await padron.DidNotReceive().ConsultarPersonaAsync(Arg.Any<AfipOptions>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SinCertificado_FallaConMensajeLegible()
    {
        var res = await Build(conCertificado: false).ConsultarCuitAsync("30711111111");

        Assert.False(res.Success);
        Assert.Contains("certificado", res.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CuitExistente_DevuelveDatosYCondicionSugerida()
    {
        var padron = Substitute.For<IPadronService>();
        padron.ConsultarPersonaAsync(Arg.Any<AfipOptions>(), 30711111111, Arg.Any<CancellationToken>())
            .Returns(new PadronPersona
            {
                RazonSocial = "EMPRESA PORTUARIA S.A.",
                Domicilio = "Calle 1 100, Bahía Blanca",
                CondicionIvaSugeridaId = 1,
                EsPersonaJuridica = true
            });

        var res = await Build(padron).ConsultarCuitAsync("30-71111111-1");   // acepta guiones

        Assert.True(res.Success);
        Assert.Equal("EMPRESA PORTUARIA S.A.", res.Data!.RazonSocial);
        Assert.Equal(1, res.Data.CondicionIvaId);
    }

    [Fact]
    public async Task CuitInexistente_DevuelveOkConDataNull()
    {
        var padron = Substitute.For<IPadronService>();
        padron.ConsultarPersonaAsync(Arg.Any<AfipOptions>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns((PadronPersona?)null);

        var res = await Build(padron).ConsultarCuitAsync("30711111111");

        Assert.True(res.Success);
        Assert.Null(res.Data);
    }

    [Fact]
    public async Task EndToEnd_ConMockPadronClient_DevuelveLaPersonaDemo()
    {
        var ticket = Substitute.For<ITicketProvider>();
        ticket.GetTicketAsync(Arg.Any<string>(), Arg.Any<AfipOptions>(), Arg.Any<CancellationToken>())
            .Returns(new AfipTicket("tok", "sig", DateTime.Now.AddHours(12)));
        var service = Build(new PadronService(ticket, new MockPadronClient()));

        var res = await service.ConsultarCuitAsync("30711111111");

        Assert.True(res.Success);
        Assert.Equal("EMPRESA DEMO S.A.", res.Data!.RazonSocial);
        Assert.Equal(1, res.Data.CondicionIvaId);
    }
}
