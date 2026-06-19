using PuertoBB.Core.Common;
using PuertoBB.Core.Enums;
using PuertoBB.Services.Negocio;
using Xunit;

namespace PuertoBB.Tests;

public class EstadoReciboHelperTests
{
    private static readonly DateTime Hoy = new(2026, 6, 5);
    private static readonly DateTime Ayer = new(2026, 6, 4);

    /// <summary>Stub de <see cref="IReciboEstadoView"/> para probar la derivación sin tocar la base.</summary>
    private sealed record Vista(
        EstadoFiscal EstadoFiscal,
        DateTime FechaVencimientoPago,
        DateTime? FechaPago = null,
        DateTime? FechaIncobrable = null,
        DateTime? FechaEnvioMail = null,
        string? UltimoErrorMail = null,
        string? UltimoErrorCae = null,
        string CAE = "",
        bool TieneNotaCredito = false) : IReciboEstadoView;

    [Fact]
    public void EstaVencido_True_SiEmitidoVencioYSigueImpago()
    {
        Assert.True(EstadoReciboHelper.EstaVencido(new Vista(EstadoFiscal.Emitido, new DateTime(2026, 5, 1)), Hoy));
        // "Enviado" (mail enviado) sigue siendo Emitido en el eje fiscal y puede estar vencido.
        Assert.True(EstadoReciboHelper.EstaVencido(new Vista(EstadoFiscal.Emitido, Ayer, FechaEnvioMail: Ayer), Hoy));
    }

    [Fact]
    public void EstaVencido_False_SiPagadoIncobrableOAnulado()
    {
        Assert.False(EstadoReciboHelper.EstaVencido(new Vista(EstadoFiscal.Emitido, new DateTime(2026, 1, 1), FechaPago: Ayer), Hoy));
        Assert.False(EstadoReciboHelper.EstaVencido(new Vista(EstadoFiscal.Emitido, new DateTime(2026, 1, 1), FechaIncobrable: Ayer), Hoy));
        Assert.False(EstadoReciboHelper.EstaVencido(new Vista(EstadoFiscal.Anulado, new DateTime(2026, 1, 1)), Hoy));
    }

    [Fact]
    public void DiasAtraso_CalculaDiferencia()
        => Assert.Equal(35, EstadoReciboHelper.DiasAtraso(new Vista(EstadoFiscal.Emitido, new DateTime(2026, 5, 1)), Hoy));

    [Fact]
    public void EtiquetaEstado_PrioridadFiscalMasCobro()
    {
        // Anulado → Incobrable → Pagado → Pendiente → Vencido → Emitido
        Assert.Equal("Anulado",    EstadoReciboHelper.EtiquetaEstado(new Vista(EstadoFiscal.Anulado, new DateTime(2026, 5, 1)), Hoy));
        Assert.Equal("Incobrable", EstadoReciboHelper.EtiquetaEstado(new Vista(EstadoFiscal.Emitido, new DateTime(2026, 5, 1), FechaIncobrable: Ayer), Hoy));
        Assert.Equal("Pagado",     EstadoReciboHelper.EtiquetaEstado(new Vista(EstadoFiscal.Emitido, new DateTime(2026, 5, 1), FechaPago: Ayer), Hoy));
        Assert.Equal("Pendiente",  EstadoReciboHelper.EtiquetaEstado(new Vista(EstadoFiscal.Pendiente, new DateTime(2026, 5, 1)), Hoy));
        Assert.Equal("Vencido",    EstadoReciboHelper.EtiquetaEstado(new Vista(EstadoFiscal.Emitido, new DateTime(2026, 5, 1)), Hoy));
        // Emitido enviado pero NO vencido: la columna Estado dice "Emitido" (el envío va en su propia columna).
        Assert.Equal("Emitido",    EstadoReciboHelper.EtiquetaEstado(new Vista(EstadoFiscal.Emitido, new DateTime(2026, 12, 1), FechaEnvioMail: Ayer), Hoy));
    }

    [Fact]
    public void EtiquetaEnvio_SoloMail()
    {
        Assert.Equal("Enviado",    EstadoReciboHelper.EtiquetaEnvio(new Vista(EstadoFiscal.Emitido, new DateTime(2026, 12, 1), FechaEnvioMail: Ayer)));
        Assert.Equal("Mail falló", EstadoReciboHelper.EtiquetaEnvio(new Vista(EstadoFiscal.Emitido, new DateTime(2026, 12, 1), UltimoErrorMail: "timeout")));
        Assert.Equal("Sin enviar", EstadoReciboHelper.EtiquetaEnvio(new Vista(EstadoFiscal.Emitido, new DateTime(2026, 12, 1))));
        Assert.Equal("—",          EstadoReciboHelper.EtiquetaEnvio(new Vista(EstadoFiscal.Pendiente, new DateTime(2026, 12, 1))));
        // Anulado refleja el envío de la NC (su traza se persiste en FechaEnvioMail/UltimoErrorMail).
        Assert.Equal("Sin enviar", EstadoReciboHelper.EtiquetaEnvio(new Vista(EstadoFiscal.Anulado, new DateTime(2026, 12, 1))));
        Assert.Equal("Enviado",    EstadoReciboHelper.EtiquetaEnvio(new Vista(EstadoFiscal.Anulado, new DateTime(2026, 12, 1), FechaEnvioMail: Ayer)));
    }

    [Fact]
    public void EnviadoYPagado_SonEjesIndependientes()
    {
        // Combinación antes imposible con el enum lineal: pagado y con mail enviado a la vez.
        var v = new Vista(EstadoFiscal.Emitido, new DateTime(2026, 12, 1), FechaPago: Ayer, FechaEnvioMail: Ayer);
        Assert.Equal("Pagado",  EstadoReciboHelper.EtiquetaEstado(v, Hoy)); // eje cobro
        Assert.Equal("Enviado", EstadoReciboHelper.EtiquetaEnvio(v));       // eje envío
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
