using Afip.Documentos.Pdf;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PuertoBB.Core.Common;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Enums;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Afip;
using PuertoBB.Core.Models.Resultados;
using PuertoBB.Infrastructure.Data;
using PuertoBB.Services.Negocio;
using PuertoBB.Services.Pdf;
using PuertoBB.Tests.TestSupport;
using QuestPDF.Infrastructure;
using Xunit;
using CpRepos = PuertoBB.Infrastructure.Repositories.CamaraPortuaria;
using CmRepos = PuertoBB.Infrastructure.Repositories.CentroMaritimo;
using CmAgencia = PuertoBB.Core.Entities.CentroMaritimo.Agencia;
using CmRecibo = PuertoBB.Core.Entities.CentroMaritimo.Recibo;
using CpGrupo = PuertoBB.Core.Entities.CamaraPortuaria.GrupoFacturacion;

namespace PuertoBB.Tests;

public class CamaraEmisionTests
{
    static CamaraEmisionTests() => QuestPDF.Settings.License = LicenseType.Community;

    private static IAfipService AfipOk()
    {
        var afip = Substitute.For<IAfipService>();
        long n = 0;
        afip.ObtenerCAEAsync(Arg.Any<ComprobanteAfipRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => ServiceResult<CaeResult>.Ok(new CaeResult
            {
                NumeroComprobante = ++n,
                Cae = $"TEST{n:D8}",
                FechaVencimientoCae = DateTime.Today.AddDays(10),
            }));
        return afip;
    }

    private static IMailService MailOk()
    {
        var mail = Substitute.For<IMailService>();
        mail.EnviarReciboAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<bool>.Ok(true));
        return mail;
    }

    private static CamaraPortuariaReciboService BuildService(CamaraPortuariaDbContext db, IMailService mail, IAfipService? afip = null)
        => new(
            new CpRepos.ReciboRepository(db, NullLogger<CpRepos.ReciboRepository>.Instance),
            new CpRepos.GrupoFacturacionRepository(db, NullLogger<CpRepos.GrupoFacturacionRepository>.Instance),
            new CpRepos.EmpresaRepository(db, NullLogger<CpRepos.EmpresaRepository>.Instance),
            new CpRepos.NotaDeCreditoRepository(db, NullLogger<CpRepos.NotaDeCreditoRepository>.Instance),
            new CpRepos.ConfiguracionRepository(db),
            afip ?? AfipOk(),
            new CamaraPortuariaPdfService(new AfipDocumentosService(), new FakeAfipConfigProvider()),
            mail,
            NullLogger<CamaraPortuariaReciboService>.Instance);

    /// <summary>AFIP que falla la primera solicitud de CAE y luego responde OK (para probar reintento).</summary>
    private static IAfipService AfipFallaUnaVez()
    {
        var afip = Substitute.For<IAfipService>();
        long n = 0;
        var llamadas = 0;
        afip.ObtenerCAEAsync(Arg.Any<ComprobanteAfipRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++llamadas == 1
                ? ServiceResult<CaeResult>.Fail("AFIP no disponible")
                : ServiceResult<CaeResult>.Ok(new CaeResult
                {
                    NumeroComprobante = ++n,
                    Cae = $"TEST{n:D8}",
                    FechaVencimientoCae = DateTime.Today.AddDays(10),
                }));
        return afip;
    }

    private static IMailService MailFallaUnaVez()
    {
        var mail = Substitute.For<IMailService>();
        var llamadas = 0;
        mail.EnviarReciboAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++llamadas == 1 ? ServiceResult<bool>.Fail("SMTP caído") : ServiceResult<bool>.Ok(true));
        return mail;
    }

    private static int SeedEmpresa(CamaraPortuariaDbContext db)
    {
        var e = new Empresa { Nombre = "X", RazonSocial = "X SA", Cuit = "30711111111", CreatedAt = DateTime.Now, Emails = [new EmailEmpresa { Email = "x@x.com", CreatedAt = DateTime.Now }] };
        db.Empresas.Add(e);
        db.SaveChanges();
        return e.Id;
    }

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

        var emision = await service.EmitirIndividualAsync(1, 1234m, "Cobro puntual", DateTime.Today, 2026, 6, enviarMail: true);
        Assert.True(emision.Data!.Exito);

        var reciboId = db.Recibos.Single().Id;
        var anulacion = await service.AnularReciboAsync(reciboId, enviarMail: true);

        Assert.True(anulacion.Success);
        Assert.Equal(ReciboEstado.Anulado, db.Recibos.Single().Estado);
        Assert.Equal(1, db.NotasDeCredito.Count());
    }

    [Fact]
    public async Task EmitirIndividual_SinMail_QuedaEmitidoYNoIntentaMail()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var empresaId = SeedEmpresa(db);
        var mail = MailOk();
        var service = BuildService(db, mail);

        var res = await service.EmitirIndividualAsync(empresaId, 1000m, "Cobro", DateTime.Today, 2026, 6, enviarMail: false);

        Assert.True(res.Data!.Exito);
        var recibo = db.Recibos.Single();
        Assert.Equal(ReciboEstado.Emitido, recibo.Estado);
        Assert.False(string.IsNullOrEmpty(recibo.CAE));
        Assert.Null(recibo.FechaEnvioMail);
        await mail.DidNotReceive().EnviarReciboAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmitirIndividual_FallaCae_QuedaPendiente_YReintentoLoCompleta()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var empresaId = SeedEmpresa(db);
        var service = BuildService(db, MailOk(), AfipFallaUnaVez());

        var primera = await service.EmitirIndividualAsync(empresaId, 1000m, "Cobro", DateTime.Today, 2026, 6, enviarMail: true);
        Assert.False(primera.Data!.Exito);                       // CAE falló
        var pendiente = db.Recibos.Single();
        Assert.Equal(ReciboEstado.Pendiente, pendiente.Estado);  // pero quedó persistido y reintentable
        Assert.True(string.IsNullOrEmpty(pendiente.CAE));
        Assert.NotNull(pendiente.UltimoErrorCae);

        var reintento = await service.ReintentarAsync(pendiente.Id, enviarMail: true);

        Assert.True(reintento.Data!.Exito);
        var completo = db.Recibos.Single();                      // no se duplicó
        Assert.Equal(ReciboEstado.Enviado, completo.Estado);
        Assert.False(string.IsNullOrEmpty(completo.CAE));
        Assert.Null(completo.UltimoErrorCae);
    }

    [Fact]
    public async Task Reintentar_TrasFalloMail_EnviaSinPedirNuevoCae()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var empresaId = SeedEmpresa(db);
        var afip = AfipOk();
        var service = BuildService(db, MailFallaUnaVez(), afip);

        var primera = await service.EmitirIndividualAsync(empresaId, 1000m, "Cobro", DateTime.Today, 2026, 6, enviarMail: true);
        Assert.True(primera.Data!.Exito);                        // CAE OK
        Assert.NotNull(primera.Data.ErrorMail);                  // pero el mail falló
        var emitido = db.Recibos.Single();
        Assert.Equal(ReciboEstado.Emitido, emitido.Estado);
        var numeroOriginal = emitido.NumeroComprobante;

        var reintento = await service.ReintentarAsync(emitido.Id, enviarMail: true);

        Assert.True(reintento.Data!.Exito);
        Assert.Null(reintento.Data.ErrorMail);
        var enviado = db.Recibos.Single();
        Assert.Equal(ReciboEstado.Enviado, enviado.Estado);
        Assert.Equal(numeroOriginal, enviado.NumeroComprobante); // mismo CAE: no se pidió otro
        await afip.Received(1).ObtenerCAEAsync(Arg.Any<ComprobanteAfipRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmitirIndividual_MismoPeriodoDosVeces_NoDuplica()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var empresaId = SeedEmpresa(db);
        var afip = AfipOk();
        var service = BuildService(db, MailOk(), afip);

        var primera = await service.EmitirIndividualAsync(empresaId, 1000m, "Cobro", DateTime.Today, 2026, 6, enviarMail: true);
        var segunda = await service.EmitirIndividualAsync(empresaId, 1000m, "Cobro", DateTime.Today, 2026, 6, enviarMail: true);

        Assert.True(primera.Data!.Exito);
        Assert.False(segunda.Data!.Exito);                       // "ya existe"
        Assert.Single(db.Recibos);                               // no se duplicó
        await afip.Received(1).ObtenerCAEAsync(Arg.Any<ComprobanteAfipRequest>(), Arg.Any<CancellationToken>());
    }
}

public class CentroCierreTests
{
    static CentroCierreTests() => QuestPDF.Settings.License = LicenseType.Community;

    private static IAfipService AfipOk()
    {
        var afip = Substitute.For<IAfipService>();
        long n = 0;
        afip.ObtenerCAEAsync(Arg.Any<ComprobanteAfipRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => ServiceResult<CaeResult>.Ok(new CaeResult
            {
                NumeroComprobante = ++n,
                Cae = $"TEST{n:D8}",
                FechaVencimientoCae = DateTime.Today.AddDays(10),
            }));
        return afip;
    }

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
            AfipOk(),
            new CentroMaritimoPdfService(new PdfMerger(), new AfipDocumentosService(), new FakeAfipConfigProvider()),
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

    [Fact]
    public async Task GetCierrePeriodo_AgrupaPorAgencia_MapeaEstadoSegunRecibo()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        var ag1 = new CmAgencia { Nombre = "Norte",  RazonSocial = "N",  Cuit = "30700000001", CreatedAt = DateTime.Now };
        var ag2 = new CmAgencia { Nombre = "Centro", RazonSocial = "C",  Cuit = "30700000002", CreatedAt = DateTime.Now };
        var ag3 = new CmAgencia { Nombre = "Sur",    RazonSocial = "S",  Cuit = "30700000003", CreatedAt = DateTime.Now };
        var barco = new Barco { Nombre = "Don Pedro", CreatedAt = DateTime.Now };
        db.Agencias.AddRange(ag1, ag2, ag3);
        db.Barcos.Add(barco);
        db.SaveChanges();

        // ag1: Emitido (recibo persistido, mail no enviado)
        var reciboEmitido = new CmRecibo
        {
            AgenciaId = ag1.Id, PeriodoAnio = 2026, PeriodoMes = 6,
            Importe = 3000m, Detalle = "Vouchers Nros: 1, 2",
            EsConsolidadoVouchers = true,
            PuntoDeVenta = 1, TipoComprobante = TipoComprobante.Recibo, CodigoAfip = 211,
            NumeroComprobante = 101, CAE = "12345678901234",
            FechaVencimientoCAE = DateTime.Today.AddDays(10),
            FechaEmision = DateTime.Today, FechaVencimientoPago = DateTime.Today.AddDays(30),
            Estado = ReciboEstado.Emitido, CreatedAt = DateTime.Now
        };
        // ag2: Completo (recibo enviado)
        var reciboCompleto = new CmRecibo
        {
            AgenciaId = ag2.Id, PeriodoAnio = 2026, PeriodoMes = 6,
            Importe = 1500m, Detalle = "Vouchers Nros: 3",
            EsConsolidadoVouchers = true,
            PuntoDeVenta = 1, TipoComprobante = TipoComprobante.Recibo, CodigoAfip = 211,
            NumeroComprobante = 102, CAE = "12345678901235",
            FechaVencimientoCAE = DateTime.Today.AddDays(10),
            FechaEmision = DateTime.Today, FechaVencimientoPago = DateTime.Today.AddDays(30),
            Estado = ReciboEstado.Enviado, CreatedAt = DateTime.Now
        };
        db.Recibos.AddRange(reciboEmitido, reciboCompleto);
        db.SaveChanges();

        db.Vouchers.AddRange(
            // ag1 → recibo Emitido
            new Voucher { AgenciaId = ag1.Id, BarcoId = barco.Id, Numero = 1, Importe = 1000m, Fecha = new DateTime(2026, 6, 5),  PeriodoAnio = 2026, PeriodoMes = 6, ReciboId = reciboEmitido.Id, CreatedAt = DateTime.Now },
            new Voucher { AgenciaId = ag1.Id, BarcoId = barco.Id, Numero = 2, Importe = 2000m, Fecha = new DateTime(2026, 6, 8),  PeriodoAnio = 2026, PeriodoMes = 6, ReciboId = reciboEmitido.Id, CreatedAt = DateTime.Now },
            // ag2 → recibo Completo
            new Voucher { AgenciaId = ag2.Id, BarcoId = barco.Id, Numero = 3, Importe = 1500m, Fecha = new DateTime(2026, 6, 9),  PeriodoAnio = 2026, PeriodoMes = 6, ReciboId = reciboCompleto.Id, CreatedAt = DateTime.Now },
            // ag3 → pendiente (sin recibo)
            new Voucher { AgenciaId = ag3.Id, BarcoId = barco.Id, Numero = 4, Importe = 500m,  Fecha = new DateTime(2026, 6, 11), PeriodoAnio = 2026, PeriodoMes = 6, CreatedAt = DateTime.Now },
            new Voucher { AgenciaId = ag3.Id, BarcoId = barco.Id, Numero = 5, Importe = 700m,  Fecha = new DateTime(2026, 6, 14), PeriodoAnio = 2026, PeriodoMes = 6, CreatedAt = DateTime.Now });
        db.SaveChanges();

        var service = new VoucherService(
            new CmRepos.VoucherRepository(db, NullLogger<CmRepos.VoucherRepository>.Instance),
            new CmRepos.ContadorVoucherRepository(db),
            new CmRepos.AgenciaRepository(db, NullLogger<CmRepos.AgenciaRepository>.Instance),
            new CmRepos.BarcoRepository(db, NullLogger<CmRepos.BarcoRepository>.Instance),
            NullLogger<VoucherService>.Instance);

        var res = await service.GetCierrePeriodoAsync(2026, 6);

        Assert.True(res.Success);
        var lista = res.Data!.ToList();
        Assert.Equal(3, lista.Count);

        var rNorte  = lista.Single(a => a.AgenciaNombre == "Norte");
        var rCentro = lista.Single(a => a.AgenciaNombre == "Centro");
        var rSur    = lista.Single(a => a.AgenciaNombre == "Sur");

        Assert.Equal(EstadoCierreAgencia.Emitido,   rNorte.Estado);
        Assert.Equal(EstadoCierreAgencia.Completo,  rCentro.Estado);
        Assert.Equal(EstadoCierreAgencia.Pendiente, rSur.Estado);

        Assert.Equal(2, rNorte.Vouchers.Count);
        Assert.Equal(3000m, rNorte.Total);
        Assert.Equal(101, rNorte.NumeroComprobante);

        Assert.Single(rCentro.Vouchers);
        Assert.Equal(1500m, rCentro.Total);

        Assert.Equal(2, rSur.Vouchers.Count);
        Assert.Equal(1200m, rSur.Total);
        Assert.Null(rSur.NumeroComprobante);
        Assert.Null(rSur.ReciboId);
    }
}
