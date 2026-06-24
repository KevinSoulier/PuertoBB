using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PuertoBB.Core.Exceptions;
using PuertoBB.Infrastructure.Data;
using PuertoBB.Tests.TestSupport;
using Xunit;
using Cp = PuertoBB.Core.Entities.CamaraPortuaria;
using Cm = PuertoBB.Core.Entities.CentroMaritimo;
using CpRepos = PuertoBB.Infrastructure.Repositories.CamaraPortuaria;
using CmRepos = PuertoBB.Infrastructure.Repositories.CentroMaritimo;

namespace PuertoBB.Tests;

public class CamaraRepositorioTests
{
    [Fact]
    public async Task Recibo_IndiceUnicoDeEmision_BloqueaDuplicados()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        db.Clientes.Add(new Cliente { Id = 1, Nombre = "E", RazonSocial = "E", Cuit = "30711234561", CreatedAt = DateTime.Now });
        db.Grupos.Add(new GrupoFacturacion { Id = 5, Nombre = "G", Importe = 100, CreatedAt = DateTime.Now });
        await db.SaveChangesAsync();

        var repo = new CpRepos.ReciboRepository(db, NullLogger<CpRepos.ReciboRepository>.Instance);
        Recibo Nuevo() => new()
        {
            ClienteId = 1, PeriodoAnio = 2026, PeriodoMes = 6,
            EmisionGrupo = new EmisionGrupo { GrupoFacturacionId = 5, ClienteId = 1, PeriodoAnio = 2026, PeriodoMes = 6, CreatedAt = DateTime.Now },
            Importe = 100, Detalle = "x", CAE = "1", FechaEmision = DateTime.Today, CreatedAt = DateTime.Now
        };

        await repo.AddAsync(Nuevo());
        await Assert.ThrowsAsync<ReciboException>(() => repo.AddAsync(Nuevo()));
        Assert.True(await repo.ExisteAsync(1, 5, 2026, 6));
        Assert.False(await repo.ExisteAsync(1, 5, 2026, 7));
    }

    [Fact]
    public async Task Recibo_IndiceNumeracionAfip_BloqueaNumeroDuplicado()
    {
        // P2-5: el índice único (PuntoDeVenta, NumeroComprobante, CodigoAfip) es la última
        // defensa contra una numeración AFIP duplicada; los Pendientes (Nro=0) quedan afuera.
        using var fx = SqliteTestDb.CreateCamara(out var db);
        db.Clientes.Add(new Cliente { Id = 1, Nombre = "E", RazonSocial = "E", Cuit = "30711234561", CreatedAt = DateTime.Now });
        await db.SaveChangesAsync();

        var repo = new CpRepos.ReciboRepository(db, NullLogger<CpRepos.ReciboRepository>.Instance);
        Recibo Nuevo(long numero) => new()
        {
            ClienteId = 1, PeriodoAnio = 2026, PeriodoMes = 6,
            Importe = 100, Detalle = "x", FechaEmision = DateTime.Today, CreatedAt = DateTime.Now,
            PuntoDeVenta = 1, CodigoAfip = 15, NumeroComprobante = numero, CAE = numero > 0 ? "1" : ""
        };

        await repo.AddAsync(Nuevo(0));
        await repo.AddAsync(Nuevo(0));               // dos Pendientes sin número: permitidos (filtro)
        await repo.AddAsync(Nuevo(10));
        Assert.Equal(3, db.Recibos.Count());

        await Assert.ThrowsAsync<ReciboException>(() => repo.AddAsync(Nuevo(10)));  // mismo (PV, Nro, Código)
    }

    [Fact]
    public async Task ReciboIndividual_YReciboDeGrupo_ConvivenEnElMismoPeriodo()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        db.Clientes.Add(new Cliente { Id = 1, Nombre = "E", RazonSocial = "E", Cuit = "30711234561", CreatedAt = DateTime.Now });
        db.Grupos.Add(new GrupoFacturacion { Id = 5, Nombre = "G", Importe = 100, CreatedAt = DateTime.Now });
        await db.SaveChangesAsync();

        var repo = new CpRepos.ReciboRepository(db, NullLogger<CpRepos.ReciboRepository>.Instance);
        // El individual debe estar en Pendiente para que FiltrarPorClave(grupoId:null) lo retorne (P1-4).
        await repo.AddAsync(new Recibo
        {
            ClienteId = 1, PeriodoAnio = 2026, PeriodoMes = 6,
            Importe = 50, Detalle = "individual", FechaEmision = DateTime.Today, CreatedAt = DateTime.Now,
            EstadoFiscal = PuertoBB.Core.Enums.EstadoFiscal.Pendiente
        });
        await repo.AddAsync(new Recibo
        {
            ClienteId = 1, PeriodoAnio = 2026, PeriodoMes = 6,
            EmisionGrupo = new EmisionGrupo { GrupoFacturacionId = 5, ClienteId = 1, PeriodoAnio = 2026, PeriodoMes = 6, CreatedAt = DateTime.Now },
            Importe = 100, Detalle = "grupo", CAE = "2", FechaEmision = DateTime.Today, CreatedAt = DateTime.Now
        });

        Assert.True(await repo.ExisteAsync(1, null, 2026, 6));  // Pendiente individual encontrado
        Assert.True(await repo.ExisteAsync(1, 5, 2026, 6));
        var individual = await repo.GetPorClaveAsync(1, null, 2026, 6);
        Assert.NotNull(individual);
        Assert.Equal("individual", individual!.Detalle);
    }

    [Fact]
    public async Task GetSinTracking_RefrescaPuntoDeVentaActivo_AunDesdeContextoDeLargaVida()
    {
        // Reproduce el bug de "Probar conexión": MarcarActivo escribe por un DbContext y el provider
        // AFIP lee por OTRO (capturado, de larga vida). Con tracking, la lectura queda stale; sin
        // tracking, refleja el cambio.
        // El seed de fábrica ya trae Configuracion Id=1 con el PV Id=1 ("Principal") activo.
        // Agregamos un segundo PV inactivo para alternar.
        using var fx = SqliteTestDb.CreateCamara(out var dbEscritura);
        dbEscritura.PuntosDeVenta.Add(new PuntoDeVenta
        {
            Id = 2, ConfiguracionId = 1, Nombre = "PV2", Numero = 2, Activo = false, CreatedAt = DateTime.Now
        });
        await dbEscritura.SaveChangesAsync();

        // Segundo contexto sobre la MISMA base: simula el DbContext capturado por AfipConfigProvider.
        var optionsLectura = new DbContextOptionsBuilder<CamaraPortuariaDbContext>()
            .UseSqlite(fx.Connection).Options;
        using var dbLectura = new CamaraPortuariaDbContext(optionsLectura);
        var repoLectura = new CpRepos.ConfiguracionRepository(dbLectura);

        // Primer GetAsync (con tracking): ceba el tracker del contexto de lectura con PV1 activo.
        Assert.Equal(1, (await repoLectura.GetAsync()).PuntoDeVentaActivo!.Id);

        // Cambio el activo a PV2 desde el OTRO contexto (como hace MarcarActivo en la UI).
        await new CpRepos.ConfiguracionRepository(dbEscritura).MarcarPuntoDeVentaActivoAsync(2);

        // Con tracking, el contexto de lectura sigue viendo PV1 (el bug que estamos arreglando)…
        Assert.Equal(1, (await repoLectura.GetAsync()).PuntoDeVentaActivo!.Id);
        // …sin tracking, refleja el estado actual: PV2.
        Assert.Equal(2, (await repoLectura.GetSinTrackingAsync()).PuntoDeVentaActivo!.Id);
    }

    [Fact]
    public async Task BorrarGrupo_ConRecibos_CascadeaRelacion_YRecibosSobreviven()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        db.Clientes.Add(new Cliente { Id = 1, Nombre = "E", RazonSocial = "E", Cuit = "30711234561", CreatedAt = DateTime.Now });
        db.Grupos.Add(new GrupoFacturacion { Id = 5, Nombre = "G", Importe = 100, CreatedAt = DateTime.Now });
        await db.SaveChangesAsync();

        var recibos = new CpRepos.ReciboRepository(db, NullLogger<CpRepos.ReciboRepository>.Instance);
        await recibos.AddAsync(new Recibo
        {
            ClienteId = 1, PeriodoAnio = 2026, PeriodoMes = 6,
            EmisionGrupo = new EmisionGrupo { GrupoFacturacionId = 5, ClienteId = 1, PeriodoAnio = 2026, PeriodoMes = 6, CreatedAt = DateTime.Now },
            Importe = 100, Detalle = "x", CAE = "1", FechaEmision = DateTime.Today, CreatedAt = DateTime.Now
        });

        var grupos = new CpRepos.GrupoFacturacionRepository(db, NullLogger<CpRepos.GrupoFacturacionRepository>.Instance);
        await grupos.DeleteAsync(5);

        Assert.Empty(db.Grupos.ToList());
        Assert.Empty(db.EmisionesGrupo.ToList());
        var recibo = Assert.Single(db.Recibos.ToList()); // el recibo (auditoría) sobrevive
        Assert.False(await recibos.ExisteAsync(1, 5, 2026, 6));
        Assert.Equal("x", recibo.Detalle);
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
        db.Clientes.Add(new Cliente { Id = 1, Nombre = "A", RazonSocial = "A", Cuit = "30700000001", CreatedAt = DateTime.Now });
        db.Recibos.Add(new Recibo
        {
            Id = 1, ClienteId = 1, PeriodoAnio = 2026, PeriodoMes = 6,
            Importe = 500, Detalle = "Vouchers", CAE = "1", FechaEmision = DateTime.Today, CreatedAt = DateTime.Now
        });
        db.Consolidaciones.Add(new Cm.Consolidacion
        {
            Id = 1, ReciboId = 1, ClienteId = 1, PeriodoAnio = 2026, PeriodoMes = 6, Pendiente = false, CreatedAt = DateTime.Now
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
        db.Clientes.Add(new Cliente { Id = 1, Nombre = "A", RazonSocial = "A", Cuit = "30700000001", CreatedAt = DateTime.Now });
        db.Barcos.Add(new Cm.Barco { Id = 1, Nombre = "B", CreatedAt = DateTime.Now });
        await db.SaveChangesAsync();

        var repo = new CmRepos.VoucherRepository(db, NullLogger<CmRepos.VoucherRepository>.Instance);
        Cm.Voucher V() => new() { ClienteId = 1, BarcoId = 1, Numero = 100, Importe = 10, Fecha = DateTime.Today, PeriodoAnio = 2026, PeriodoMes = 6, CreatedAt = DateTime.Now };
        await repo.AddAsync(V());
        await Assert.ThrowsAsync<ReciboException>(() => repo.AddAsync(V()));
    }
}
