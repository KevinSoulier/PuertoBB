using System.Collections.Generic;
using PuertoBB.Core.Mail;
using Xunit;

namespace PuertoBB.Tests;

public class PlantillaMailTests
{
    private static readonly Dictionary<string, string?> Vars = new()
    {
        ["periodo"]     = "Junio 2026",
        ["receptor"]    = "ACME SA",
        ["razonSocial"] = "Mi Empresa",
        ["comprobante"] = "Recibo",
        ["numero"]      = "0001-00000123",
        ["importe"]     = "$ 1.000,00",
    };

    [Fact]
    public void Aplicar_ReemplazaVariablesConocidas()
        => Assert.Equal("Recibo Junio 2026 — Mi Empresa",
            PlantillaMail.Aplicar("{comprobante} {periodo} — {razonSocial}", Vars));

    [Fact]
    public void Aplicar_DejaIntactasLasDesconocidas()
        => Assert.Equal("Hola ACME SA, {otra} fin",
            PlantillaMail.Aplicar("Hola {receptor}, {otra} fin", Vars));

    [Fact]
    public void Aplicar_EsInsensibleAMayusculasEnElNombre()
        => Assert.Equal("Junio 2026 ACME SA", PlantillaMail.Aplicar("{Periodo} {RECEPTOR}", Vars));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Aplicar_PlantillaVacia_DevuelveVacio(string? plantilla)
        => Assert.Equal(string.Empty, PlantillaMail.Aplicar(plantilla, Vars));

    [Fact]
    public void Aplicar_ValorNull_ReemplazaPorCadenaVacia()
        => Assert.Equal("Nro: ",
            PlantillaMail.Aplicar("Nro: {numero}", new Dictionary<string, string?> { ["numero"] = null }));

    [Fact]
    public void Defaults_ResuelvenTodasLasVariables()
    {
        var asunto = PlantillaMail.Aplicar(PlantillaMail.DefaultAsunto, Vars);
        var cuerpo = PlantillaMail.Aplicar(PlantillaMail.DefaultCuerpoTexto, Vars);
        Assert.Equal("Recibo Junio 2026 — Mi Empresa", asunto);
        Assert.DoesNotContain("{", asunto);     // no quedan tokens sin resolver
        Assert.Contains("Junio 2026", cuerpo);
        Assert.DoesNotContain("{", cuerpo);
    }

    [Fact]
    public void QuitarHtml_QuitaTagsYDecodificaEntidades()
        => Assert.Equal("Hola ACME&Co Saludos",
            PlantillaMail.QuitarHtml("<p>Hola <strong>ACME&amp;Co</strong></p><br><div>Saludos</div>"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void QuitarHtml_Vacio_DevuelveVacio(string? html)
        => Assert.Equal(string.Empty, PlantillaMail.QuitarHtml(html));
}
