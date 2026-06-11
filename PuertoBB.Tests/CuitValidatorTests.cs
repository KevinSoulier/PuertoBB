using PuertoBB.Core.Common;
using Xunit;

namespace PuertoBB.Tests;

public class CuitValidatorTests
{
    [Theory]
    [InlineData("30621973173")] // ADM Agro (SeedData)
    [InlineData("30643949381")] // Ag. Marítima Austral (SeedData)
    [InlineData("30585343427")] // Ag. Marítima Internacional (SeedData)
    public void EsValido_CuitValido_RetornaTrue(string cuit) =>
        Assert.True(CuitValidator.EsValido(cuit));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345678900")] // dígito verificador incorrecto
    [InlineData("1234567890")]  // solo 10 dígitos
    [InlineData("abcdefghijk")] // no numérico
    public void EsValido_CuitInvalido_RetornaFalse(string? cuit) =>
        Assert.False(CuitValidator.EsValido(cuit));
}
