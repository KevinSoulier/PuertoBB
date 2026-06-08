using System.Xml.Linq;
using Afip.Abstractions;
using Afip.Soap;
using Afip.Soap.Wsfe;
using Afip.Wsaa;
using Xunit;

namespace PuertoBB.Tests;

public class WsfeMapperRequestTests
{
    private static WsfeCaeRequest BaseReq(ComprobanteAsociadoWsfe? asoc = null) => new()
    {
        TipoComprobante = 11,
        PuntoDeVenta = 3,
        Numero = 42,
        Concepto = 2,
        DocTipo = 80,
        DocNro = 30711234561,
        FechaComprobante = new DateTime(2026, 6, 15),
        ImporteTotal = 12345.67m,
        ServicioDesde = 20260601,
        ServicioHasta = 20260630,
        VencimientoPago = new DateTime(2026, 7, 15),
        ComprobanteAsociado = asoc
    };

    [Fact]
    public void ToFECAERequest_TipoC_Exento_SinArrayIva()
    {
        var fe = WsfeMapper.ToFECAERequest(BaseReq());

        Assert.Equal(1, fe.FeCabReq.CantReg);
        Assert.Equal(3, fe.FeCabReq.PtoVta);
        Assert.Equal(11, fe.FeCabReq.CbteTipo);

        var det = Assert.Single(fe.FeDetReq);
        Assert.Null(det.Iva);                       // CRÍTICO: sin IVA para tipo C
        Assert.Equal(0d, det.ImpNeto);
        Assert.Equal(0d, det.ImpIVA);
        Assert.Equal(12345.67d, det.ImpOpEx, 3);    // todo exento
        Assert.Equal(12345.67d, det.ImpTotal, 3);
        Assert.Equal("PES", det.MonId);
        Assert.Equal(1d, det.MonCotiz);
        Assert.Equal(2, det.Concepto);
        Assert.Equal(80, det.DocTipo);
        Assert.Equal(42, det.CbteDesde);
        Assert.Equal(42, det.CbteHasta);
        Assert.Equal("20260615", det.CbteFch);
        Assert.Equal("20260601", det.FchServDesde);
        Assert.Equal("20260630", det.FchServHasta);
        Assert.Equal("20260715", det.FchVtoPago);
        Assert.Null(det.CbtesAsoc);                 // recibo: sin comprobante asociado
    }

    [Fact]
    public void ToFECAERequest_NotaDeCredito_IncluyeCbtesAsoc()
    {
        var asoc = new ComprobanteAsociadoWsfe { Tipo = 11, PuntoDeVenta = 3, Numero = 40, Cuit = 30999999999 };
        var fe = WsfeMapper.ToFECAERequest(BaseReq(asoc));

        var det = Assert.Single(fe.FeDetReq);
        var ca = Assert.Single(det.CbtesAsoc);
        Assert.Equal(11, ca.Tipo);
        Assert.Equal(3, ca.PtoVta);
        Assert.Equal(40, ca.Nro);
        Assert.Equal("30999999999", ca.Cuit);
    }
}

public class WsfeMapperResponseTests
{
    [Fact]
    public void ToWsfeCaeResponse_Aprobado_DevuelveCaeYNumero()
    {
        var resp = new FECAEResponse
        {
            FeCabResp = new FECAECabResponse { Resultado = "A" },
            FeDetResp = [new FECAEDetResponse { CAE = "75123456789012", CAEFchVto = "20260625", CbteDesde = 42, Resultado = "A" }]
        };

        var r = WsfeMapper.ToWsfeCaeResponse(resp, 42);

        Assert.True(r.Aprobado);
        Assert.Equal("75123456789012", r.Cae);
        Assert.Equal(new DateTime(2026, 6, 25), r.FechaVencimientoCae);
        Assert.Equal(42, r.Numero);
        Assert.Null(r.Observaciones);
    }

    [Fact]
    public void ToWsfeCaeResponse_Rechazado_DevuelveObservaciones()
    {
        var resp = new FECAEResponse
        {
            FeCabResp = new FECAECabResponse { Resultado = "R" },
            FeDetResp = [new FECAEDetResponse { Resultado = "R", Observaciones = [new Obs { Code = 10071, Msg = "No corresponde informar IVA" }] }],
            Errors = [new Err { Code = 600, Msg = "Validación fallida" }]
        };

        var r = WsfeMapper.ToWsfeCaeResponse(resp, 42);

        Assert.False(r.Aprobado);
        Assert.Contains("10071", r.Observaciones);
        Assert.Contains("600", r.Observaciones);
    }
}

public class TraBuilderTests
{
    [Fact]
    public void GenerarTraXml_TieneEstructuraYServicio()
    {
        var xml = TraBuilder.GenerarTraXml("wsfe");
        var doc = XDocument.Parse(xml);

        Assert.Equal("loginTicketRequest", doc.Root!.Name.LocalName);
        Assert.NotNull(doc.Descendants("uniqueId").FirstOrDefault());
        Assert.NotNull(doc.Descendants("generationTime").FirstOrDefault());
        Assert.NotNull(doc.Descendants("expirationTime").FirstOrDefault());
        Assert.Equal("wsfe", doc.Descendants("service").First().Value);

        var gen = DateTime.Parse(doc.Descendants("generationTime").First().Value);
        var exp = DateTime.Parse(doc.Descendants("expirationTime").First().Value);
        Assert.True(exp > gen);
    }
}
