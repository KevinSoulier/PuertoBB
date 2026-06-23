using PuertoBB.Core.Enums;
using PuertoBB.Services.Common;
using Xunit;
using Recibo = PuertoBB.Core.Entities.CamaraPortuaria.Recibo;

namespace PuertoBB.Tests;

/// <summary>
/// Cubre la búsqueda de texto en memoria de la sección "Control" (<see cref="ControlBusqueda"/>):
/// matchea contra las 6 columnas visibles tal como se muestran (agencia/empresa, comprobante, período,
/// importe, emisión y vencimiento), es case-insensitive y pagina sobre el resultado filtrado.
/// </summary>
public class ControlBusquedaTests
{
    // Fecha fija para que el conteo de vencidos sea determinístico (todos los vencimientos son anteriores).
    private static readonly DateTime Hoy = new(2026, 6, 22);

    private static Recibo Nuevo(string nombre, int anio, int mes, decimal importe, int pv, long numero, DateTime emision, DateTime venc)
        => new()
        {
            ReceptorNombre = nombre, ReceptorRazonSocial = nombre + " SA", ReceptorCuit = "30711111111",
            PeriodoAnio = anio, PeriodoMes = mes, Importe = importe,
            EstadoFiscal = EstadoFiscal.Emitido,
            PuntoDeVenta = pv, NumeroComprobante = numero, CAE = $"CAE{numero:D8}",
            FechaEmision = emision, FechaVencimientoPago = venc,
            CreatedAt = DateTime.Now,
        };

    private static IReadOnlyList<Recibo> Seed() =>
    [
        Nuevo("Naviera Sur",      2026, 6,  1000m, 1, 12,    new DateTime(2026, 3, 15), new DateTime(2026, 4, 20)),
        Nuevo("Transporte Norte", 2025, 11, 2500m, 2, 12345, new DateTime(2025, 12, 1), new DateTime(2025, 12, 31)),
        Nuevo("Logistica Este",   2026, 1,  999m,  3, 7,     new DateTime(2026, 1, 5),  new DateTime(2026, 2, 10)),
    ];

    [Theory]
    [InlineData("naviera",    "Naviera Sur")]       // por nombre (agencia/empresa)
    [InlineData("NAVIERA",    "Naviera Sur")]       // case-insensitive
    [InlineData("junio",      "Naviera Sur")]       // por período (nombre del mes)
    [InlineData("1.000",      "Naviera Sur")]       // por importe formateado
    [InlineData("15/03/2026", "Naviera Sur")]       // por fecha de emisión
    [InlineData("20/04/2026", "Naviera Sur")]       // por fecha de vencimiento
    [InlineData("12345",      "Transporte Norte")]  // por número de comprobante
    [InlineData("0002-",      "Transporte Norte")]  // por comprobante formateado (PV)
    [InlineData("0003-",      "Logistica Este")]
    public void Busca_PorCadaColumnaVisible(string texto, string esperado)
    {
        var page = ControlBusqueda.Filtrar(Seed(), texto, pagina: 1, tamanio: 50, Hoy);
        Assert.Equal(esperado, Assert.Single(page.Items).ReceptorNombre);
    }

    [Fact]
    public void Busca_PorAnio_DevuelveTodosLosDeEseAnio()
    {
        var page = ControlBusqueda.Filtrar(Seed(), "2026", pagina: 1, tamanio: 50, Hoy);

        Assert.Equal(2, page.Total);
        Assert.Equal(
            new[] { "Logistica Este", "Naviera Sur" }.OrderBy(x => x),
            page.Items.Select(r => r.ReceptorNombre).OrderBy(x => x));
    }

    [Fact]
    public void Paginado_EnMemoria_RespetaTamanioYPagina()
    {
        var p1 = ControlBusqueda.Filtrar(Seed(), "2026", pagina: 1, tamanio: 1, Hoy);
        var p2 = ControlBusqueda.Filtrar(Seed(), "2026", pagina: 2, tamanio: 1, Hoy);

        Assert.Equal(2, p1.Total);
        Assert.Equal(2, p1.TotalPaginas);
        Assert.Single(p1.Items);
        Assert.Single(p2.Items);
        Assert.NotEqual(p1.Items[0].NumeroComprobante, p2.Items[0].NumeroComprobante); // sin solapamiento
    }

    [Fact]
    public void SinTexto_DevuelveTodos()
    {
        var page = ControlBusqueda.Filtrar(Seed(), "   ", pagina: 1, tamanio: 50, Hoy);
        Assert.Equal(3, page.Total);
    }

    [Fact]
    public void Vencidos_SeCuentanSobreElResultadoFiltrado()
    {
        // Todos los vencimientos sembrados son anteriores a Hoy → los 3 cuentan como vencidos.
        var page = ControlBusqueda.Filtrar(Seed(), null, pagina: 1, tamanio: 50, Hoy);
        Assert.Equal(3, page.Vencidos);
    }
}
