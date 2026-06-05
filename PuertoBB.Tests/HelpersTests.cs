using PuertoBB.Core.Common;
using PuertoBB.Core.Enums;
using PuertoBB.Services.Negocio;
using Xunit;

namespace PuertoBB.Tests;

public class EstadoReciboHelperTests
{
    private static readonly DateTime Hoy = new(2026, 6, 5);

    [Fact]
    public void EstaVencido_True_SiVencioYNoEstaPagado()
    {
        Assert.True(EstadoReciboHelper.EstaVencido(ReciboEstado.Emitido, new DateTime(2026, 5, 1), Hoy));
        Assert.True(EstadoReciboHelper.EstaVencido(ReciboEstado.Enviado, new DateTime(2026, 6, 4), Hoy));
    }

    [Fact]
    public void EstaVencido_False_SiPagadoOAnulado()
    {
        Assert.False(EstadoReciboHelper.EstaVencido(ReciboEstado.Pagado, new DateTime(2026, 1, 1), Hoy));
        Assert.False(EstadoReciboHelper.EstaVencido(ReciboEstado.Anulado, new DateTime(2026, 1, 1), Hoy));
    }

    [Fact]
    public void DiasAtraso_CalculaDiferencia()
        => Assert.Equal(35, EstadoReciboHelper.DiasAtraso(ReciboEstado.Emitido, new DateTime(2026, 5, 1), Hoy));

    [Fact]
    public void EtiquetaEstado_DevuelveVencido_CuandoCorresponde()
    {
        Assert.Equal("Vencido", EstadoReciboHelper.EtiquetaEstado(ReciboEstado.Emitido, new DateTime(2026, 5, 1), Hoy));
        Assert.Equal("Enviado", EstadoReciboHelper.EtiquetaEstado(ReciboEstado.Enviado, new DateTime(2026, 12, 1), Hoy));
    }
}

public class PeriodoHelperTests
{
    [Fact]
    public void PrimerYUltimoDia_FormatoYyyyMMdd()
    {
        Assert.Equal(20260201, PeriodoHelper.PrimerDia(2026, 2));
        Assert.Equal(20260228, PeriodoHelper.UltimoDia(2026, 2));
        Assert.Equal(20240229, PeriodoHelper.UltimoDia(2024, 2)); // bisiesto
    }

    [Fact]
    public void SoloDigitos_EliminaGuiones()
        => Assert.Equal("30711234561", PeriodoHelper.SoloDigitos("30-71123456-1"));
}
