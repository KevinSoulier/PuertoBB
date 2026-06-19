using System.Xml.Linq;
using Afip.Abstractions;
using Afip.Soap;
using Afip.Soap.Wsfe;
using Afip.Wsaa;
using PuertoBB.Services.Common;
using Xunit;

namespace PuertoBB.Tests;

/// <summary>
/// Contrato de <see cref="Formato.ParseReceptorDoc"/>: única fuente de verdad del documento del
/// receptor. Tanto la request a WSFE (AfipService.ToAfipRequest) como el QR del PDF
/// (CentroMaritimo/CamaraPortuariaPdfService) derivan (DocTipo, DocNro) de acá, así no pueden
/// divergir y romper la validación del QR ("tipo y número de documento del receptor no se corresponde").
/// </summary>
public class FormatoReceptorDocTests
{
    [Theory]
    [InlineData("30711234561", 80, 30711234561L)]   // CUIT real → 80
    [InlineData("30-71123456-1", 80, 30711234561L)] // con guiones/espacios → mismos dígitos
    public void ParseReceptorDoc_ConCuit_DevuelveTipo80(string cuit, int tipoEsperado, long nroEsperado)
    {
        var (tipo, nro) = Formato.ParseReceptorDoc(cuit);
        Assert.Equal(tipoEsperado, tipo);
        Assert.Equal(nroEsperado, nro);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseReceptorDoc_SinCuit_ConsumidorFinal_99Cero(string? cuit)
    {
        var (tipo, nro) = Formato.ParseReceptorDoc(cuit);
        Assert.Equal(99, tipo);   // Consumidor Final (no 80+0, que AFIP rechaza para tipo C)
        Assert.Equal(0L, nro);
    }
}

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
        CondicionIvaReceptorId = 1,
        FechaComprobante = new DateTime(2026, 6, 15),
        ImporteTotal = 12345.67m,
        ServicioDesde = 20260601,
        ServicioHasta = 20260630,
        VencimientoPago = new DateTime(2026, 7, 15),
        ComprobanteAsociado = asoc
    };

    [Fact]
    public void ToFECAERequest_TipoC_TotalEnNeto_SinArrayIva()
    {
        var fe = WsfeMapper.ToFECAERequest(BaseReq());

        Assert.Equal(1, fe.FeCabReq.CantReg);
        Assert.Equal(3, fe.FeCabReq.PtoVta);
        Assert.Equal(11, fe.FeCabReq.CbteTipo);

        var det = Assert.Single(fe.FeDetReq);
        Assert.Null(det.Iva);                       // CRÍTICO: sin IVA para tipo C
        Assert.Equal(12345.67d, det.ImpNeto, 3);    // tipo C: el total va en Neto (no se discrimina IVA)
        Assert.Equal(0d, det.ImpIVA);
        Assert.Equal(0d, det.ImpOpEx);              // tipo C: exento SIEMPRE 0 (rechazo 10044 si no)
        Assert.Equal(12345.67d, det.ImpTotal, 3);   // ImpTotal == ImpNeto + ImpTrib (rechazo 10048 si no)
        Assert.Equal("PES", det.MonId);
        Assert.Equal(1d, det.MonCotiz);
        Assert.Equal(2, det.Concepto);
        Assert.Equal(80, det.DocTipo);
        Assert.Equal(1, det.CondicionIVAReceptorId);   // RG 5616: obligatorio (0 = rechazo 10242)
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

public class WsfeMapperParametrosTests
{
    [Fact]
    public void ToPuntosVenta_ParseaBloqueadoYFechaBaja()
    {
        var resp = new FEPtoVentaResponse
        {
            ResultGet =
            [
                new PtoVenta { Nro = 1, EmisionTipo = "CAE",  Bloqueado = "N", FchBaja = "NULL" },
                new PtoVenta { Nro = 2, EmisionTipo = "CAEA", Bloqueado = "S", FchBaja = "20260101" },
            ]
        };

        var pvs = WsfeMapper.ToPuntosVenta(resp);

        Assert.Equal(2, pvs.Count);
        Assert.False(pvs[0].Bloqueado);
        Assert.Null(pvs[0].FechaBaja);
        Assert.True(pvs[1].Bloqueado);
        Assert.Equal(new DateTime(2026, 1, 1), pvs[1].FechaBaja);
        Assert.Equal("CAEA", pvs[1].EmisionTipo);
    }

    [Fact]
    public void ToPuntosVenta_SinResultados_O_Error602_DevuelveVacio()
    {
        Assert.Empty(WsfeMapper.ToPuntosVenta(new FEPtoVentaResponse()));
        Assert.Empty(WsfeMapper.ToPuntosVenta(new FEPtoVentaResponse
        {
            Errors = [new Err { Code = 602, Msg = "Sin resultados" }]
        }));
    }

    [Fact]
    public void ToPuntosVenta_ErrorReal_LanzaConCodigo()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => WsfeMapper.ToPuntosVenta(new FEPtoVentaResponse
        {
            Errors = [new Err { Code = 600, Msg = "Credenciales inválidas" }]
        }));
        Assert.Contains("[600]", ex.Message);
    }

    [Fact]
    public void ToTiposComprobante_ParseaVigencias()
    {
        var resp = new CbteTipoResponse
        {
            ResultGet = [new CbteTipo { Id = 15, Desc = "Recibo C", FchDesde = "20100917", FchHasta = "NULL" }]
        };

        var tipos = WsfeMapper.ToTiposComprobante(resp);

        var t = Assert.Single(tipos);
        Assert.Equal(15, t.Id);
        Assert.Equal(new DateTime(2010, 9, 17), t.VigenteDesde);
        Assert.Null(t.VigenteHasta);
    }

    [Fact]
    public void ToCondicionesIvaReceptor_MapeaIdDescripcionYClase()
    {
        var resp = new CondicionIvaReceptorResponse
        {
            ResultGet = [new CondicionIvaReceptor { Id = 6, Desc = "Responsable Monotributo", Cmp_Clase = "C" }]
        };

        var conds = WsfeMapper.ToCondicionesIvaReceptor(resp);

        var c = Assert.Single(conds);
        Assert.Equal(6, c.Id);
        Assert.Equal("Responsable Monotributo", c.Descripcion);
        Assert.Equal("C", c.ClaseComprobante);
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
