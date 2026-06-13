using Afip;
using Afip.Abstractions;
using Afip.Padron;
using Afip.Soap;
using Afip.Soap.Padron;
using Afip.Wsaa;
using NSubstitute;
using Xunit;

namespace PuertoBB.Tests;

/// <summary>Mapeo de la constancia de inscripción y derivación de condición IVA (RG 5616).</summary>
public class PadronMapperTests
{
    [Fact]
    public void Juridica_ConIva30_DevuelveRazonSocialYCondicion1()
    {
        var resp = new personaReturn
        {
            datosGenerales = new datosGenerales
            {
                razonSocial = "EMPRESA PORTUARIA S.A.",
                tipoPersona = "JURIDICA",
                domicilioFiscal = new domicilio { direccion = "Calle 1 100", localidad = "Bahía Blanca", descripcionProvincia = "Buenos Aires", codPostal = "8000" }
            },
            datosRegimenGeneral = new datosRegimenGeneral
            {
                impuesto = [new impuesto { idImpuesto = 30, estadoImpuesto = "ACTIVO", descripcionImpuesto = "IVA" }]
            }
        };

        var p = PadronMapper.ToPersona(resp)!;

        Assert.Equal("EMPRESA PORTUARIA S.A.", p.RazonSocial);
        Assert.True(p.EsPersonaJuridica);
        Assert.Equal(1, p.CondicionIvaSugeridaId);
        Assert.Equal("Calle 1 100, Bahía Blanca, Buenos Aires (CP 8000)", p.Domicilio);
        Assert.Empty(p.Observaciones);
    }

    [Fact]
    public void Monotributista_DevuelveCondicion6()
    {
        var resp = new personaReturn
        {
            datosGenerales = new datosGenerales { apellido = "PEREZ", nombre = "JUAN", tipoPersona = "FISICA" },
            datosMonotributo = new datosMonotributo()
        };

        var p = PadronMapper.ToPersona(resp)!;

        Assert.Equal("PEREZ JUAN", p.RazonSocial);
        Assert.False(p.EsPersonaJuridica);
        Assert.Equal(6, p.CondicionIvaSugeridaId);
    }

    [Fact]
    public void Exento_Impuesto32_DevuelveCondicion4()
    {
        var resp = new personaReturn
        {
            datosGenerales = new datosGenerales { razonSocial = "CLUB X", tipoPersona = "JURIDICA" },
            datosRegimenGeneral = new datosRegimenGeneral
            {
                impuesto = [new impuesto { idImpuesto = 32, estadoImpuesto = "ACTIVO" }]
            }
        };

        Assert.Equal(4, PadronMapper.ToPersona(resp)!.CondicionIvaSugeridaId);
    }

    [Fact]
    public void SinImpuestosDeIva_DevuelveCondicion15()
    {
        var resp = new personaReturn
        {
            datosGenerales = new datosGenerales { razonSocial = "ENTIDAD Y", tipoPersona = "JURIDICA" },
            datosRegimenGeneral = new datosRegimenGeneral
            {
                impuesto = [new impuesto { idImpuesto = 11, estadoImpuesto = "ACTIVO", descripcionImpuesto = "GANANCIAS" }]
            }
        };

        Assert.Equal(15, PadronMapper.ToPersona(resp)!.CondicionIvaSugeridaId);
    }

    [Fact]
    public void ErrorConstancia_DevuelveParcialConObservaciones_SinCondicion()
    {
        var resp = new personaReturn
        {
            errorConstancia = new errorConstancia
            {
                apellido = "GOMEZ",
                nombre = "ANA",
                error = ["El contribuyente no se encuentra alcanzado por la constancia de inscripción."]
            }
        };

        var p = PadronMapper.ToPersona(resp)!;

        Assert.Equal("GOMEZ ANA", p.RazonSocial);
        Assert.Null(p.CondicionIvaSugeridaId);
        Assert.Single(p.Observaciones);
    }

    [Fact]
    public void SinDatos_DevuelveNull()
    {
        Assert.Null(PadronMapper.ToPersona(null));
        Assert.Null(PadronMapper.ToPersona(new personaReturn()));
    }
}

/// <summary>La fachada del padrón pide el TA del servicio correcto y delega en el cliente.</summary>
public class PadronServiceTests
{
    [Fact]
    public async Task ConsultarPersona_PideTicketDeConstanciaInscripcion_YDelega()
    {
        var options = new AfipOptions { Cuit = "30700000007", UsarHomologacion = true };
        var ticket = Substitute.For<ITicketProvider>();
        ticket.GetTicketAsync(Arg.Any<string>(), Arg.Any<AfipOptions>(), Arg.Any<CancellationToken>())
            .Returns(new AfipTicket("tok", "sig", DateTime.Now.AddHours(12)));
        var cliente = Substitute.For<IPadronClient>();
        cliente.ConsultarAsync("tok", "sig", "30700000007", 30711111111, true, Arg.Any<CancellationToken>())
            .Returns(new PadronPersona { RazonSocial = "X SA", CondicionIvaSugeridaId = 1 });

        var service = new PadronService(ticket, cliente);
        var p = await service.ConsultarPersonaAsync(options, 30711111111);

        Assert.Equal("X SA", p!.RazonSocial);
        await ticket.Received(1).GetTicketAsync("ws_sr_constancia_inscripcion", options, Arg.Any<CancellationToken>());
    }
}
