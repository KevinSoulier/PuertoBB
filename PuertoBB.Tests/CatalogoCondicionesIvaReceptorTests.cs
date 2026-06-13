using PuertoBB.Core.Afip;
using Xunit;

namespace PuertoBB.Tests;

public class CatalogoCondicionesIvaReceptorTests
{
    [Fact]
    public void Todas_ContieneLosCodigosOficiales()
    {
        var codigos = CatalogoCondicionesIvaReceptor.Todas.Select(c => c.Codigo).ToArray();
        Assert.Equal([1, 4, 5, 6, 7, 8, 9, 10, 13, 15, 16], codigos);
    }

    [Fact]
    public void PorCodigo_DevuelveLaCondicion_YNullSiNoExiste()
    {
        Assert.Equal("IVA Responsable Inscripto", CatalogoCondicionesIvaReceptor.PorCodigo(1)!.Descripcion);
        Assert.Equal("Responsable Monotributo", CatalogoCondicionesIvaReceptor.PorCodigo(6)!.Descripcion);
        Assert.Null(CatalogoCondicionesIvaReceptor.PorCodigo(99));
    }

    [Fact]
    public void Descripcion_DerivaTextoDelCodigo_YNullParaNullODesconocido()
    {
        Assert.Equal("IVA Sujeto Exento", CatalogoCondicionesIvaReceptor.Descripcion(4));
        Assert.Null(CatalogoCondicionesIvaReceptor.Descripcion(null));
        Assert.Null(CatalogoCondicionesIvaReceptor.Descripcion(99));
    }

    [Fact]
    public void Display_CombinaCodigoYDescripcion()
    {
        Assert.Equal("1 — IVA Responsable Inscripto", CatalogoCondicionesIvaReceptor.PorCodigo(1)!.Display);
    }
}
