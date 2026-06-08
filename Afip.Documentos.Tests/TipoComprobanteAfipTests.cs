using Afip.Documentos.Catalogo;
using Xunit;

namespace Afip.Documentos.Tests;

public class TipoComprobanteAfipTests
{
    [Theory]
    [InlineData(1, "A")]
    [InlineData(6, "B")]
    [InlineData(11, "C")]
    [InlineData(13, "C")]
    [InlineData(15, "C")]
    public void Letra_EsCorrecta(int codigo, string esperada)
        => Assert.Equal(esperada, TipoComprobanteAfip.Letra(codigo));

    [Theory]
    [InlineData(1, "FACTURA")]
    [InlineData(6, "FACTURA")]
    [InlineData(11, "FACTURA")]
    [InlineData(3, "NOTA DE CRÉDITO")]
    [InlineData(13, "NOTA DE CRÉDITO")]
    [InlineData(2, "NOTA DE DÉBITO")]
    public void Nombre_EsCorrect(int codigo, string esperado)
        => Assert.Equal(esperado, TipoComprobanteAfip.Nombre(codigo));
}
