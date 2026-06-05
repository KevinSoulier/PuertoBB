using Microsoft.Extensions.Logging.Abstractions;
using PuertoBB.Core.Exceptions;
using PuertoBB.Services.Afip;
using PuertoBB.Tests.TestSupport;
using Xunit;
using Cp = PuertoBB.Core.Entities.CamaraPortuaria;
using Cm = PuertoBB.Core.Entities.CentroMaritimo;
using CpRepos = PuertoBB.Infrastructure.Repositories.CamaraPortuaria;
using CmRepos = PuertoBB.Infrastructure.Repositories.CentroMaritimo;

namespace PuertoBB.Tests;

public class FakeAfipServiceTests
{
    [Fact]
    public async Task ObtenerCAE_NumeraSecuencialmentePorPuntoYTipo()
    {
        var afip = new FakeAfipService(NullLogger<FakeAfipService>.Instance);
        var req = new Core.Models.Afip.ComprobanteAfipRequest
        {
            TipoComprobante = Core.Enums.TipoComprobante.Recibo,
            CodigoAfip = 11, PuntoDeVenta = 1, CuitReceptor = "30711234561",
            ImporteTotal = 1000, FechaEmision = DateTime.Today,
            PeriodoServicioDesde = 20260601, PeriodoServicioHasta = 20260630,
            FechaVencimientoPago = DateTime.Today.AddDays(30)
        };

        var r1 = await afip.ObtenerCAEAsync(req);
        var r2 = await afip.ObtenerCAEAsync(req);

        Assert.True(r1.Success);
        Assert.Equal(1, r1.Data!.NumeroComprobante);
        Assert.Equal(2, r2.Data!.NumeroComprobante);
        Assert.False(string.IsNullOrWhiteSpace(r1.Data.Cae));
    }
}

public class CamaraRepositorioTests
{
    [Fact]
    public async Task Recibo_IndiceUnico_BloqueaDuplicados()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        db.Empresas.Add(new Cp.Empresa { Id = 1, Nombre = "E", RazonSocial = "E", Cuit = "30711234561", CreatedAt = DateTime.Now });
        db.Grupos.Add(new Cp.GrupoFacturacion { Id = 5, Nombre = "G", Importe = 100, CreatedAt = DateTime.Now });
        await db.SaveChangesAsync();

        var repo = new CpRepos.ReciboRepository(db, NullLogger<CpRepos.ReciboRepository>.Instance);
        Cp.Recibo Nuevo() => new()
        {
            EmpresaId = 1, GrupoFacturacionId = 5, PeriodoAnio = 2026, PeriodoMes = 6,
            Importe = 100, Detalle = "x", CAE = "1", FechaEmision = DateTime.Today, CreatedAt = DateTime.Now
        };

        await repo.AddAsync(Nuevo());
        await Assert.ThrowsAsync<ReciboException>(() => repo.AddAsync(Nuevo()));
        Assert.True(await repo.ExisteAsync(1, 5, 2026, 6));
        Assert.False(await repo.ExisteAsync(1, 5, 2026, 7));
    }
}

public class CentroRepositorioTests
{
    [Fact]
    public async Task ContadorVoucher_IncrementaSecuencial()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        var repo = new CmRepos.ContadorVoucherRepository(db);

        Assert.Equal(1, await repo.ObtenerSiguienteNumeroAsync());
        Assert.Equal(2, await repo.ObtenerSiguienteNumeroAsync());
        Assert.Equal(3, await repo.ObtenerSiguienteNumeroAsync());
    }

    [Fact]
    public async Task ExisteConsolidado_DetectaReciboDeVouchers()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        db.Agencias.Add(new Cm.Agencia { Id = 1, Nombre = "A", RazonSocial = "A", Cuit = "30700000001", CreatedAt = DateTime.Now });
        db.Recibos.Add(new Cm.Recibo
        {
            Id = 1, AgenciaId = 1, PeriodoAnio = 2026, PeriodoMes = 6, EsConsolidadoVouchers = true,
            Importe = 500, Detalle = "Vouchers", CAE = "1", FechaEmision = DateTime.Today, CreatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();

        var repo = new CmRepos.ReciboRepository(db, NullLogger<CmRepos.ReciboRepository>.Instance);
        Assert.True(await repo.ExisteConsolidadoAsync(1, 2026, 6));
        Assert.False(await repo.ExisteConsolidadoAsync(1, 2026, 7));
    }

    [Fact]
    public async Task Voucher_NumeroUnico_BloqueaDuplicados()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        db.Agencias.Add(new Cm.Agencia { Id = 1, Nombre = "A", RazonSocial = "A", Cuit = "30700000001", CreatedAt = DateTime.Now });
        db.Barcos.Add(new Cm.Barco { Id = 1, Nombre = "B", CreatedAt = DateTime.Now });
        await db.SaveChangesAsync();

        var repo = new CmRepos.VoucherRepository(db, NullLogger<CmRepos.VoucherRepository>.Instance);
        Cm.Voucher V() => new() { AgenciaId = 1, BarcoId = 1, Numero = 100, Importe = 10, Fecha = DateTime.Today, PeriodoAnio = 2026, PeriodoMes = 6, CreatedAt = DateTime.Now };
        await repo.AddAsync(V());
        await Assert.ThrowsAsync<ReciboException>(() => repo.AddAsync(V()));
    }
}
