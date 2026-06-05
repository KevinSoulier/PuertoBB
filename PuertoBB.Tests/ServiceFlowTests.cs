using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PuertoBB.Core.Common;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Enums;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Infrastructure.Data;
using PuertoBB.Services.Afip;
using PuertoBB.Services.Negocio;
using PuertoBB.Services.Pdf;
using PuertoBB.Tests.TestSupport;
using QuestPDF.Infrastructure;
using Xunit;
using CpRepos = PuertoBB.Infrastructure.Repositories.CamaraPortuaria;
using CmRepos = PuertoBB.Infrastructure.Repositories.CentroMaritimo;
using CmAgencia = PuertoBB.Core.Entities.CentroMaritimo.Agencia;
using CpGrupo = PuertoBB.Core.Entities.CamaraPortuaria.GrupoFacturacion;

namespace PuertoBB.Tests;

public class CamaraEmisionTests
{
    static CamaraEmisionTests() => QuestPDF.Settings.License = LicenseType.Community;

    private static IMailService MailOk()
    {
        var mail = Substitute.For<IMailService>();
        mail.EnviarReciboAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<bool>.Ok(true));
        return mail;
    }

    private static CamaraPortuariaReciboService BuildService(CamaraPortuariaDbContext db, IMailService mail)
        => new(
            new CpRepos.ReciboRepository(db, NullLogger<CpRepos.ReciboRepository>.Instance),
            new CpRepos.GrupoFacturacionRepository(db, NullLogger<CpRepos.GrupoFacturacionRepository>.Instance),
            new CpRepos.EmpresaRepository(db, NullLogger<CpRepos.EmpresaRepository>.Instance),
            new CpRepos.NotaDeCreditoRepository(db, NullLogger<CpRepos.NotaDeCreditoRepository>.Instance),
            new CpRepos.ConfiguracionRepository(db),
            new FakeAfipService(NullLogger<FakeAfipService>.Instance),
            new CamaraPortuariaPdfService(),
            mail,
            NullLogger<CamaraPortuariaReciboService>.Instance);

    private static int SeedGrupoConEmpresas(CamaraPortuariaDbContext db)
    {
        var grupo = new CpGrupo { Nombre = "Cuota", Importe = 5000m, CreatedAt = DateTime.Now };
        db.Grupos.Add(grupo);
        var e1 = new Empresa { Nombre = "Uno", RazonSocial = "Uno SA", Cuit = "30711111111", CreatedAt = DateTime.Now, Emails = [new EmailEmpresa { Email = "u@x.com", CreatedAt = DateTime.Now }] };
        var e2 = new Empresa { Nombre = "Dos", RazonSocial = "Dos SA", Cuit = "30722222222", CreatedAt = DateTime.Now, Emails = [new EmailEmpresa { Email = "d@x.com", CreatedAt = DateTime.Now }] };
        db.Empresas.AddRange(e1, e2);
        db.SaveChanges();
        db.EmpresasGrupos.AddRange(
            new EmpresaGrupo { EmpresaId = e1.Id, GrupoFacturacionId = grupo.Id, CreatedAt = DateTime.Now },
            new EmpresaGrupo { EmpresaId = e2.Id, GrupoFacturacionId = grupo.Id, CreatedAt = DateTime.Now });
        db.SaveChanges();
        return grupo.Id;
    }

    [Fact]
    public async Task EmitirMasivo_GeneraReciboPorEmpresa_ConCaeYEstadoEnviado()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var grupoId = SeedGrupoConEmpresas(db);
        var service = BuildService(db, MailOk());

        var res = await service.EmitirMasivoAsync(grupoId, 2026, 6);

        Assert.True(res.Success);
        Assert.Equal(2, res.Data!.Count);
        Assert.All(res.Data, r => Assert.True(r.Exito));
        Assert.All(res.Data, r => Assert.Null(r.ErrorMail));

        var recibos = db.Recibos.ToList();
        Assert.Equal(2, recibos.Count);
        Assert.All(recibos, r => Assert.False(string.IsNullOrEmpty(r.CAE)));
        Assert.All(recibos, r => Assert.Equal(ReciboEstado.Enviado, r.Estado));
        Assert.All(recibos, r => Assert.Equal(5000m, r.Importe));
    }

    [Fact]
    public async Task EmitirMasivo_SegundaVez_BloqueaDuplicados()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var grupoId = SeedGrupoConEmpresas(db);
        var service = BuildService(db, MailOk());

        await service.EmitirMasivoAsync(grupoId, 2026, 6);
        var segunda = await service.EmitirMasivoAsync(grupoId, 2026, 6);

        Assert.All(segunda.Data!, r => Assert.False(r.Exito));
        Assert.Equal(2, db.Recibos.Count()); // no se duplicaron
    }

    [Fact]
    public async Task EmitirMasivo_MailFalla_ReciboQuedaEmitido_ConErrorMail()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var grupoId = SeedGrupoConEmpresas(db);
        var mail = Substitute.For<IMailService>();
        mail.EnviarReciboAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<bool>.Fail("SMTP caído"));
        var service = BuildService(db, mail);

        var res = await service.EmitirMasivoAsync(grupoId, 2026, 6);

        Assert.All(res.Data!, r => Assert.True(r.Exito));            // CAE obtenido = emisión exitosa
        Assert.All(res.Data!, r => Assert.NotNull(r.ErrorMail));     // pero el mail falló
        Assert.All(db.Recibos.ToList(), r => Assert.Equal(ReciboEstado.Emitido, r.Estado));
    }

    [Fact]
    public async Task EmitirIndividual_AnularGeneraNotaDeCredito()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        db.Empresas.Add(new Empresa { Id = 1, Nombre = "X", RazonSocial = "X", Cuit = "30711111111", CreatedAt = DateTime.Now, Emails = [new EmailEmpresa { Email = "x@x.com", CreatedAt = DateTime.Now }] });
        db.SaveChanges();
        var service = BuildService(db, MailOk());

        var emision = await service.EmitirIndividualAsync(1, 1234m, "Cobro puntual", 2026, 6);
        Assert.True(emision.Data!.Exito);

        var reciboId = db.Recibos.Single().Id;
        var anulacion = await service.AnularReciboAsync(reciboId, enviarMail: true);

        Assert.True(anulacion.Success);
        Assert.Equal(ReciboEstado.Anulado, db.Recibos.Single().Estado);
        Assert.Equal(1, db.NotasDeCredito.Count());
    }
}

public class CentroCierreTests
{
    static CentroCierreTests() => QuestPDF.Settings.License = LicenseType.Community;

    private static IMailService MailOk()
    {
        var mail = Substitute.For<IMailService>();
        mail.EnviarReciboAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<bool>.Ok(true));
        return mail;
    }

    private static CentroMaritimoReciboService BuildService(CentroMaritimoDbContext db, IMailService mail)
        => new(
            new CmRepos.ReciboRepository(db, NullLogger<CmRepos.ReciboRepository>.Instance),
            new CmRepos.GrupoFacturacionRepository(db, NullLogger<CmRepos.GrupoFacturacionRepository>.Instance),
            new CmRepos.AgenciaRepository(db, NullLogger<CmRepos.AgenciaRepository>.Instance),
            new CmRepos.VoucherRepository(db, NullLogger<CmRepos.VoucherRepository>.Instance),
            new CmRepos.NotaDeCreditoRepository(db, NullLogger<CmRepos.NotaDeCreditoRepository>.Instance),
            new CmRepos.ConfiguracionRepository(db),
            new FakeAfipService(NullLogger<FakeAfipService>.Instance),
            new CentroMaritimoPdfService(),
            mail,
            NullLogger<CentroMaritimoReciboService>.Instance);

    [Fact]
    public async Task CerrarPeriodo_ConsolidaVouchers_EnUnReciboPorAgencia()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        var ag = new CmAgencia { Nombre = "Ag", RazonSocial = "Ag SA", Cuit = "30700000001", CreatedAt = DateTime.Now, Emails = [new EmailAgencia { Email = "a@x.com", CreatedAt = DateTime.Now }] };
        var barco = new Barco { Nombre = "Barco", CreatedAt = DateTime.Now };
        db.Agencias.Add(ag);
        db.Barcos.Add(barco);
        db.SaveChanges();
        db.Vouchers.AddRange(
            new Voucher { AgenciaId = ag.Id, BarcoId = barco.Id, Numero = 1, Importe = 1000m, Fecha = new DateTime(2026, 6, 10), PeriodoAnio = 2026, PeriodoMes = 6, CreatedAt = DateTime.Now },
            new Voucher { AgenciaId = ag.Id, BarcoId = barco.Id, Numero = 2, Importe = 2500m, Fecha = new DateTime(2026, 6, 12), PeriodoAnio = 2026, PeriodoMes = 6, CreatedAt = DateTime.Now });
        db.SaveChanges();

        var service = BuildService(db, MailOk());
        var res = await service.CerrarPeriodoAsync(2026, 6);

        Assert.True(res.Success);
        var r = Assert.Single(res.Data!);
        Assert.True(r.Exito);
        Assert.Equal(2, r.CantidadVouchers);
        Assert.Equal(3500m, r.Importe);

        var recibo = db.Recibos.Single();
        Assert.True(recibo.EsConsolidadoVouchers);
        Assert.Equal(3500m, recibo.Importe);
        Assert.Contains("Vouchers Nros:", recibo.Detalle);
        Assert.All(db.Vouchers.ToList(), v => Assert.Equal(recibo.Id, v.ReciboId));
    }

    [Fact]
    public async Task CerrarPeriodo_SinPendientes_NoGeneraRecibos()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        var service = BuildService(db, MailOk());
        var res = await service.CerrarPeriodoAsync(2026, 6);
        Assert.True(res.Success);
        Assert.Empty(res.Data!);
    }
}

public class VoucherServiceTests
{
    [Fact]
    public async Task CrearVoucher_AsignaNumeroYDerivaPeriodo()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        var ag = new CmAgencia { Nombre = "Ag", RazonSocial = "Ag", Cuit = "30700000001", CreatedAt = DateTime.Now };
        var barco = new Barco { Nombre = "B", CreatedAt = DateTime.Now };
        db.Agencias.Add(ag);
        db.Barcos.Add(barco);
        db.SaveChanges();

        var service = new VoucherService(
            new CmRepos.VoucherRepository(db, NullLogger<CmRepos.VoucherRepository>.Instance),
            new CmRepos.ContadorVoucherRepository(db),
            new CmRepos.AgenciaRepository(db, NullLogger<CmRepos.AgenciaRepository>.Instance),
            new CmRepos.BarcoRepository(db, NullLogger<CmRepos.BarcoRepository>.Instance),
            NullLogger<VoucherService>.Instance);

        var v1 = await service.CrearVoucherAsync(ag.Id, barco.Id, new DateTime(2026, 3, 15), 800m);
        var v2 = await service.CrearVoucherAsync(ag.Id, barco.Id, new DateTime(2026, 3, 20), 900m);

        Assert.True(v1.Success);
        Assert.Equal(1, v1.Data!.Numero);
        Assert.Equal(2, v2.Data!.Numero);
        Assert.Equal(2026, v1.Data.PeriodoAnio);
        Assert.Equal(3, v1.Data.PeriodoMes);
    }
}
