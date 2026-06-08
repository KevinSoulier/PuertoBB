using PuertoBB.Core.Afip;
using Xunit;

namespace PuertoBB.Tests;

public class CatalogoComprobantesAfipTests
{
    [Theory]
    [InlineData(15, 13)] // Recibo C  → Nota de Crédito C
    [InlineData(11, 13)] // Factura C → Nota de Crédito C
    [InlineData(9,  8)]  // Recibo B  → Nota de Crédito B
    [InlineData(6,  8)]  // Factura B → Nota de Crédito B
    [InlineData(4,  3)]  // Recibo A  → Nota de Crédito A
    [InlineData(1,  3)]  // Factura A → Nota de Crédito A
    public void NotaCreditoPara_DerivaPorClase(int codigoPrincipal, int ncEsperada)
    {
        Assert.Equal(ncEsperada, CatalogoComprobantesAfip.NotaCreditoPara(codigoPrincipal));
    }

    [Fact]
    public void NotaCreditoPara_CodigoDesconocido_DevuelveElMismo()
    {
        Assert.Equal(999, CatalogoComprobantesAfip.NotaCreditoPara(999));
    }

    [Fact]
    public void Principales_TieneRecibosYFacturasABC()
    {
        Assert.Equal(6, CatalogoComprobantesAfip.Principales.Count);
    }

    [Fact]
    public void PorCodigo_DevuelveDescripcionYDisplay()
    {
        var reciboC = CatalogoComprobantesAfip.PorCodigo(15);

        Assert.NotNull(reciboC);
        Assert.Equal("Recibo C", reciboC!.Descripcion);
        Assert.Equal(ClaseFiscal.C, reciboC.Clase);
        Assert.Equal("15 — Recibo C", reciboC.Display);
    }

    [Theory]
    [InlineData(15, "Nota de Crédito C")]
    [InlineData(9,  "Nota de Crédito B")]
    [InlineData(4,  "Nota de Crédito A")]
    public void DescripcionNotaCredito_SegunClase(int codigoPrincipal, string esperado)
    {
        Assert.Equal(esperado, CatalogoComprobantesAfip.DescripcionNotaCredito(codigoPrincipal));
    }
}
