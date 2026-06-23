using Microsoft.Extensions.Logging.Abstractions;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Enums;
using PuertoBB.Core.Models.Resultados;
using PuertoBB.Infrastructure.Data;
using PuertoBB.Tests.TestSupport;
using Xunit;
using CpRepos = PuertoBB.Infrastructure.Repositories.CamaraPortuaria;
using CmRepos = PuertoBB.Infrastructure.Repositories.CentroMaritimo;
using CmAgencia = PuertoBB.Core.Entities.CentroMaritimo.Agencia;
using CmRecibo = PuertoBB.Core.Entities.CentroMaritimo.Recibo;
using CmEmail = PuertoBB.Core.Entities.CentroMaritimo.EmailAgencia;

namespace PuertoBB.Tests;

/// <summary>
/// Cubre el paginado server-side de la sección "Control" (<c>GetControlPaginadoAsync</c>): el mapeo de
/// cada <see cref="FiltroEstadoControl"/> a recibos, el conteo total/vencidos y el Skip/Take. El predicado
/// SQL debe coincidir con <c>EstadoReciboHelper.EtiquetaEstado</c>. La búsqueda de texto vive en
/// <c>ControlBusquedaTests</c>.
/// </summary>
public class ControlPaginadoTests
{
    private static readonly DateTime Hoy = DateTime.Today;

    private static CpRepos.ReciboRepository RepoCamara(CamaraPortuariaDbContext db)
        => new(db, NullLogger<CpRepos.ReciboRepository>.Instance);

    private static int SeedEmpresa(CamaraPortuariaDbContext db)
    {
        var e = new Empresa
        {
            Nombre = "Empresa", RazonSocial = "Empresa SA", Cuit = "30711111111", CondicionIvaId = 1,
            CreatedAt = DateTime.Now, Emails = [new EmailEmpresa { Email = "e@x.com", CreatedAt = DateTime.Now }],
        };
        db.Empresas.Add(e);
        db.SaveChanges();
        return e.Id;
    }

    /// <summary>Crea un recibo en el estado deseado. El número (positivo y distinto) respeta el índice único de comprobante.</summary>
    private static Recibo NuevoRecibo(
        int empresaId, string nombre, EstadoFiscal estado, DateTime vencimiento,
        DateTime? pago = null, DateTime? incobrable = null, int mes = 6, long numero = 0)
        => new()
        {
            EmpresaId = empresaId,
            ReceptorNombre = nombre, ReceptorRazonSocial = nombre + " SA", ReceptorCuit = "30711111111",
            PeriodoAnio = 2026, PeriodoMes = mes, Importe = 1000m,
            EstadoFiscal = estado,
            PuntoDeVenta = 1, NumeroComprobante = numero, CAE = numero > 0 ? $"CAE{numero:D8}" : "",
            FechaEmision = vencimiento.AddDays(-30), FechaVencimientoPago = vencimiento,
            FechaPago = pago, FechaIncobrable = incobrable,
            CreatedAt = DateTime.Now,
        };

    /// <summary>Siembra un recibo de cada estado relevante (números distintos). Devuelve el id de la empresa.</summary>
    private static int SeedTodosLosEstados(CamaraPortuariaDbContext db)
    {
        var empresaId = SeedEmpresa(db);
        db.Recibos.AddRange(
            NuevoRecibo(empresaId, "EmitidoVigente", EstadoFiscal.Emitido, Hoy.AddDays(10), numero: 1),
            NuevoRecibo(empresaId, "Vencido",        EstadoFiscal.Emitido, Hoy.AddDays(-10), numero: 2),
            NuevoRecibo(empresaId, "Pagado",         EstadoFiscal.Emitido, Hoy.AddDays(-10), pago: Hoy.AddDays(-1), numero: 3),
            NuevoRecibo(empresaId, "Incobrable",     EstadoFiscal.Emitido, Hoy.AddDays(-10), incobrable: Hoy.AddDays(-1), numero: 4),
            NuevoRecibo(empresaId, "Anulado",        EstadoFiscal.Anulado, Hoy.AddDays(-10), numero: 5),
            NuevoRecibo(empresaId, "SinCae",         EstadoFiscal.Pendiente, Hoy.AddDays(10), numero: 0));
        db.SaveChanges();
        return empresaId;
    }

    [Fact]
    public async Task PendientesDePago_DevuelveEmitidoYVencido()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        SeedTodosLosEstados(db);
        var repo = RepoCamara(db);

        var page = await repo.GetControlPaginadoAsync(new FiltroControlPagos { Estado = FiltroEstadoControl.PendientesDePago });

        Assert.Equal(2, page.Total);
        Assert.Equal(1, page.Vencidos);
        Assert.Equal(
            new[] { "EmitidoVigente", "Vencido" }.OrderBy(x => x),
            page.Items.Select(r => r.ReceptorNombre).OrderBy(x => x));
    }

    [Theory]
    [InlineData(FiltroEstadoControl.Vencido,    "Vencido")]
    [InlineData(FiltroEstadoControl.Pagado,     "Pagado")]
    [InlineData(FiltroEstadoControl.Incobrable, "Incobrable")]
    [InlineData(FiltroEstadoControl.Anulado,    "Anulado")]
    public async Task FiltroPorEstado_DevuelveSoloEseEstado(FiltroEstadoControl estado, string esperado)
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        SeedTodosLosEstados(db);
        var repo = RepoCamara(db);

        var page = await repo.GetControlPaginadoAsync(new FiltroControlPagos { Estado = estado });

        Assert.Equal(esperado, Assert.Single(page.Items).ReceptorNombre);
    }

    [Fact]
    public async Task Todos_ExcluyeElPendienteSinCae()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        SeedTodosLosEstados(db);
        var repo = RepoCamara(db);

        var page = await repo.GetControlPaginadoAsync(new FiltroControlPagos { Estado = FiltroEstadoControl.Todos });

        // Control solo muestra comprobantes con CAE: el borrador "SinCae" (Pendiente) queda fuera.
        Assert.Equal(5, page.Total);
        Assert.DoesNotContain(page.Items, r => r.ReceptorNombre == "SinCae");
    }

    [Fact]
    public async Task Paginado_RecortaPaginasSinSolapar()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var empresaId = SeedEmpresa(db);
        // 5 emitidos impagos en períodos distintos → orden determinístico (PeriodoMes desc).
        for (var mes = 1; mes <= 5; mes++)
            db.Recibos.Add(NuevoRecibo(empresaId, $"R{mes}", EstadoFiscal.Emitido, Hoy.AddDays(10), mes: mes, numero: mes));
        db.SaveChanges();
        var repo = RepoCamara(db);

        var p1 = await repo.GetControlPaginadoAsync(new FiltroControlPagos { Estado = FiltroEstadoControl.Todos, Pagina = 1, TamanioPagina = 2 });
        var p2 = await repo.GetControlPaginadoAsync(new FiltroControlPagos { Estado = FiltroEstadoControl.Todos, Pagina = 2, TamanioPagina = 2 });
        var p3 = await repo.GetControlPaginadoAsync(new FiltroControlPagos { Estado = FiltroEstadoControl.Todos, Pagina = 3, TamanioPagina = 2 });

        Assert.Equal(5, p1.Total);
        Assert.Equal(3, p1.TotalPaginas);
        Assert.Equal(2, p1.Items.Count);
        Assert.Equal(2, p2.Items.Count);
        Assert.Single(p3.Items);
        var ids = p1.Items.Concat(p2.Items).Concat(p3.Items).Select(r => r.Id).ToList();
        Assert.Equal(5, ids.Distinct().Count()); // sin solapamientos
        // Orden por período descendente.
        Assert.Equal("R5", p1.Items[0].ReceptorNombre);
    }

    [Fact]
    public async Task Paginado_PaginaFueraDeRango_SeRecortaALaUltima()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        SeedTodosLosEstados(db);
        var repo = RepoCamara(db);

        var page = await repo.GetControlPaginadoAsync(new FiltroControlPagos
        {
            Estado = FiltroEstadoControl.Todos, Pagina = 99, TamanioPagina = 2,
        });

        Assert.Equal(5, page.Total); // 6 sembrados − 1 "SinCae" (Pendiente, sin CAE)
        Assert.Equal(3, page.TotalPaginas);
        Assert.Equal(3, page.Pagina); // recortada a la última
    }

    [Fact]
    public async Task CentroMaritimo_PendientesDePago_FiltraSoloEmitido()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        var ag = new CmAgencia
        {
            Nombre = "Naviera Sur", RazonSocial = "Naviera Sur SA", Cuit = "30700000001", CondicionIvaId = 1,
            CreatedAt = DateTime.Now, Emails = [new CmEmail { Email = "a@x.com", CreatedAt = DateTime.Now }],
        };
        db.Agencias.Add(ag);
        db.SaveChanges();

        CmRecibo Nuevo(string nombre, EstadoFiscal estado, DateTime venc, DateTime? pago, long numero) => new()
        {
            AgenciaId = ag.Id,
            ReceptorNombre = nombre, ReceptorRazonSocial = nombre + " SA", ReceptorCuit = "30700000001",
            PeriodoAnio = 2026, PeriodoMes = 6, Importe = 1000m, EstadoFiscal = estado,
            PuntoDeVenta = 1, NumeroComprobante = numero, CAE = $"CAE{numero:D8}",
            FechaEmision = venc.AddDays(-30), FechaVencimientoPago = venc, FechaPago = pago,
            CreatedAt = DateTime.Now,
        };
        db.Recibos.AddRange(
            Nuevo("Emitido", EstadoFiscal.Emitido, Hoy.AddDays(10), null, 1),
            Nuevo("Pagado", EstadoFiscal.Emitido, Hoy.AddDays(-10), Hoy.AddDays(-1), 2));
        db.SaveChanges();
        var repo = new CmRepos.ReciboRepository(db, NullLogger<CmRepos.ReciboRepository>.Instance);

        var pendientes = await repo.GetControlPaginadoAsync(new FiltroControlPagos { Estado = FiltroEstadoControl.PendientesDePago });
        Assert.Equal("Emitido", Assert.Single(pendientes.Items).ReceptorNombre);
    }
}
