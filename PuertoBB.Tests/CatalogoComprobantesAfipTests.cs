using PuertoBB.Core.Afip;
using Xunit;

namespace PuertoBB.Tests;

public class CatalogoComprobantesAfipTests
{
    [Theory]
    [InlineData(15, 13)] // Recibo C  → Nota de Crédito C
    [InlineData(11, 13)] // Factura C → Nota de Crédito C
    public void NotaCreditoPara_DerivaPorClase(int codigoPrincipal, int ncEsperada)
    {
        Assert.Equal(ncEsperada, CatalogoComprobantesAfip.NotaCreditoPara(codigoPrincipal));
    }

    [Theory]
    [InlineData(999)] // código fuera del catálogo
    [InlineData(9)]   // Recibo B: ya no es seleccionable (solo clase C)
    [InlineData(1)]   // Factura A: ya no es seleccionable (solo clase C)
    public void NotaCreditoPara_CodigoNoSeleccionable_DevuelveElMismo(int codigo)
    {
        Assert.Equal(codigo, CatalogoComprobantesAfip.NotaCreditoPara(codigo));
    }

    [Fact]
    public void Principales_SoloClaseC()
    {
        Assert.Equal(2, CatalogoComprobantesAfip.Principales.Count);
        Assert.All(CatalogoComprobantesAfip.Principales, c => Assert.Equal(ClaseFiscal.C, c.Clase));
        Assert.Contains(CatalogoComprobantesAfip.Principales, c => c.Codigo == 15);
        Assert.Contains(CatalogoComprobantesAfip.Principales, c => c.Codigo == 11);
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
    [InlineData(11, "Nota de Crédito C")]
    public void DescripcionNotaCredito_SegunClase(int codigoPrincipal, string esperado)
    {
        Assert.Equal(esperado, CatalogoComprobantesAfip.DescripcionNotaCredito(codigoPrincipal));
    }
}
