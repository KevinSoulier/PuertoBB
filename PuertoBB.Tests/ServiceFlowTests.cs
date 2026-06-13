using Afip.Documentos.Pdf;
using Microsoft.EntityFrameworkCore;
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
        var e = new Empresa { Nombre = "X", RazonSocial = "X SA", Cuit = "30711111111", CondicionIvaId = 1, CreatedAt = DateTime.Now, Emails = [new EmailEmpresa { Email = "x@x.com", CreatedAt = DateTime.Now }] };
        db.Empresas.Add(e);
        db.SaveChanges();
        return e.Id;
    }

    private static int SeedGrupoConEmpresas(CamaraPortuariaDbContext db)
    {
        var grupo = new CpGrupo { Nombre = "Cuota", Importe = 5000m, CreatedAt = DateTime.Now };
        db.Grupos.Add(grupo);
        var e1 = new Empresa { Nombre = "Uno", RazonSocial = "Uno SA", Cuit = "30711111111", CondicionIvaId = 1, CreatedAt = DateTime.Now, Emails = [new EmailEmpresa { Email = "u@x.com", CreatedAt = DateTime.Now }] };
        var e2 = new Empresa { Nombre = "Dos", RazonSocial = "Dos SA", Cuit = "30722222222", CondicionIvaId = 1, CreatedAt = DateTime.Now, Emails = [new EmailEmpresa { Email = "d@x.com", CreatedAt = DateTime.Now }] };
        db.Empresas.AddRange(e1, e2);
        db.SaveChanges();
        db.EmpresasGrupos.AddRange(
            new EmpresaGrupo { EmpresaId = e1.Id, GrupoFacturacionId = grupo.Id, CreatedAt = DateTime.Now },
            new EmpresaGrupo { EmpresaId = e2.Id, GrupoFacturacionId = grupo.Id, CreatedAt = DateTime.Now });
        db.SaveChanges();
        return grupo.Id;
    }

    // ---- RG 5616: condición frente al IVA del receptor ----

    [Fact]
    public async Task EmitirIndividual_EmpresaSinCondicionIva_FallaSinLlamarAfip()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var e = new Empresa { Nombre = "SinCond", RazonSocial = "SinCond SA", Cuit = "30733333334", CreatedAt = DateTime.Now, Emails = [new EmailEmpresa { Email = "s@x.com", CreatedAt = DateTime.Now }] };
        db.Empresas.Add(e);
        db.SaveChanges();
        var afip = AfipOk();
        var service = BuildService(db, MailOk(), afip);

        var res = await service.EmitirIndividualAsync(e.Id, 1000m, "Cuota", DateTime.Today, 2026, 6, enviarMail: false);

        Assert.False(res.Data!.Exito);
        Assert.Contains("RG 5616", res.Data.ErrorEmision);
        await afip.DidNotReceive().ObtenerCAEAsync(Arg.Any<ComprobanteAfipRequest>(), Arg.Any<CancellationToken>());
        Assert.Equal(ReciboEstado.Pendiente, db.Recibos.Single().Estado);   // queda reintentable
    }

    [Fact]
    public async Task EmitirIndividual_CopiaSnapshotDeCondicionIva()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var empresaId = SeedEmpresa(db);   // CondicionIvaId = 1
        var service = BuildService(db, MailOk());

        var res = await service.EmitirIndividualAsync(empresaId, 1000m, "Cuota", DateTime.Today, 2026, 6, enviarMail: false);

        Assert.True(res.Data!.Exito);
        var recibo = db.Recibos.Single();
        Assert.Equal(1, recibo.ReceptorCondicionIvaId);
        Assert.Equal("IVA Responsable Inscripto", recibo.ReceptorCondicionIva);   // texto derivado del catálogo
    }

    [Fact]
    public async Task Anular_PasaCondicionIvaDelSnapshotALaNotaDeCredito()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var empresaId = SeedEmpresa(db);
        var afip = AfipOk();
        var service = BuildService(db, MailOk(), afip);
        Assert.True((await service.EmitirIndividualAsync(empresaId, 1000m, "Cuota", DateTime.Today, 2026, 6, enviarMail: false)).Data!.Exito);

        var anulacion = await service.AnularReciboAsync(db.Recibos.Single().Id, enviarMail: false);

        Assert.True(anulacion.Success);
        var reqNc = (ComprobanteAfipRequest)afip.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IAfipService.ObtenerCAEAsync))
            .Last().GetArguments()[0]!;
        Assert.Equal(TipoComprobante.NotaDeCredito, reqNc.TipoComprobante);
        Assert.Equal(1, reqNc.CondicionIvaReceptorId);
    }

    [Fact]
    public async Task Reintentar_ComprobanteYaEmitidoEnAfip_RecuperaSinReemitir()
    {
        // B: tras un intento previo fallido, el CAE pudo haberse autorizado sin registrarse.
        // El reintento debe RECUPERARLO de AFIP, no re-emitir (evita duplicar).
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var empresaId = SeedEmpresa(db);

        var afip = Substitute.For<IAfipService>();
        afip.ObtenerCAEAsync(Arg.Any<ComprobanteAfipRequest>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CaeResult>.Fail("AFIP no disponible"));        // 1er intento falla
        afip.RecuperarComprobanteAsync(Arg.Any<ComprobanteAfipRequest>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CaeResult?>.Ok(new CaeResult
            {
                NumeroComprobante = 8, Cae = "CAERECUP", FechaVencimientoCae = DateTime.Today.AddDays(10)
            }));
        var service = BuildService(db, MailOk(), afip);

        // 1er intento: AFIP falla → recibo Pendiente con UltimoErrorCae (población de riesgo).
        await service.EmitirIndividualAsync(empresaId, 1000m, "Cuota", DateTime.Today, 2026, 6, enviarMail: false);
        var reciboId = db.Recibos.Single().Id;
        Assert.Equal(ReciboEstado.Pendiente, db.Recibos.Single().Estado);
        Assert.NotNull(db.Recibos.Single().UltimoErrorCae);

        // 2do intento: recupera el comprobante ya emitido en AFIP.
        var res = await service.ReintentarAsync(reciboId, enviarMail: false);

        Assert.True(res.Data!.Exito);
        var recibo = db.Recibos.Single();
        Assert.Equal(ReciboEstado.Emitido, recibo.Estado);
        Assert.Equal("CAERECUP", recibo.CAE);
        Assert.Equal(8, recibo.NumeroComprobante);
        // ObtenerCAE solo en el 1er intento; el 2do recuperó sin re-emitir → no duplica.
        await afip.Received(1).ObtenerCAEAsync(Arg.Any<ComprobanteAfipRequest>(), Arg.Any<CancellationToken>());
        await afip.Received(1).RecuperarComprobanteAsync(Arg.Any<ComprobanteAfipRequest>(), Arg.Any<CancellationToken>());
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
        // N-6: invariante fiscal — el total persistido SIEMPRE es la suma de las líneas.
        Assert.All(recibos, r => Assert.Equal(db.RecibosLineas.Where(l => l.ReciboId == r.Id).Sum(l => l.Importe), r.Importe));
        Assert.All(recibos, r => Assert.Equal(ReciboEstado.Enviado, r.Estado));
        Assert.All(recibos, r => Assert.Equal(5000m, r.Importe));
    }

    [Fact]
    public async Task EmitirMasivo_CopiaSnapshotReceptor_YCreaEmisionGrupo()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var grupoId = SeedGrupoConEmpresas(db);
        var service = BuildService(db, MailOk());

        await service.EmitirMasivoAsync(grupoId, 2026, 6);

        var recibos = db.Recibos.ToList();
        var emisiones = db.EmisionesGrupo.ToList();

        // El recibo es autocontenido: snapshot fiscal del receptor copiado al emitir.
        var uno = recibos.Single(r => r.ReceptorNombre == "Uno");
        Assert.Equal("Uno SA", uno.ReceptorRazonSocial);
        Assert.Equal("30711111111", uno.ReceptorCuit);

        // El vínculo con el grupo vive en la entidad de relación, con período/receptor coincidentes.
        Assert.Equal(2, emisiones.Count);
        Assert.All(emisiones, e => Assert.Equal(grupoId, e.GrupoFacturacionId));
        Assert.All(recibos, r => Assert.Contains(emisiones,
            e => e.ReciboId == r.Id && e.EmpresaId == r.EmpresaId && e.PeriodoAnio == r.PeriodoAnio && e.PeriodoMes == r.PeriodoMes));
    }

    [Fact]
    public async Task EmitirMasivo_PersisteLineasSnapshot_ConTotalIgualSuma()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var grupoId = SeedGrupoConEmpresas(db);
        var service = BuildService(db, MailOk());

        await service.EmitirMasivoAsync(grupoId, 2026, 6);

        var recibos = db.Recibos.ToList();
        var lineas = db.RecibosLineas.ToList();
        // Cada recibo (emisión por grupo = mono-ítem) tiene al menos una línea persistida (snapshot).
        Assert.All(recibos, r => Assert.Contains(lineas, l => l.ReciboId == r.Id));
        // El total del recibo es exactamente la suma de sus líneas.
        Assert.All(recibos, r => Assert.Equal(r.Importe, lineas.Where(l => l.ReciboId == r.Id).Sum(l => l.Importe)));
    }

    [Fact]
    public async Task EmitirMasivo_ConLineasDeGrupo_MaterializaUnaLineaPorItem()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var grupo = new CpGrupo
        {
            Nombre = "Cuota+extras",
            Importe = 0m, // se recalcula al sumar las líneas
            CreatedAt = DateTime.Now,
            Lineas =
            [
                new PuertoBB.Core.Entities.CamaraPortuaria.GrupoFacturacionLinea { Descripcion = "Cuota mensual", Cantidad = 1, PrecioUnitario = 5000m, Importe = 5000m, Orden = 0, CreatedAt = DateTime.Now },
                new PuertoBB.Core.Entities.CamaraPortuaria.GrupoFacturacionLinea { Descripcion = "Aporte extra",  Cantidad = 2, PrecioUnitario = 1500m, Importe = 3000m, Orden = 1, CreatedAt = DateTime.Now },
            ]
        };
        db.Grupos.Add(grupo);
        var emp = new Empresa { Nombre = "Uno", RazonSocial = "Uno SA", Cuit = "30711111111", CondicionIvaId = 1, CreatedAt = DateTime.Now, Emails = [new EmailEmpresa { Email = "u@x.com", CreatedAt = DateTime.Now }] };
        db.Empresas.Add(emp);
        db.SaveChanges();
        db.EmpresasGrupos.Add(new EmpresaGrupo { EmpresaId = emp.Id, GrupoFacturacionId = grupo.Id, CreatedAt = DateTime.Now });
        db.SaveChanges();
        var service = BuildService(db, MailOk());

        var res = await service.EmitirMasivoAsync(grupo.Id, 2026, 6);

        Assert.True(res.Success);
        var recibo = Assert.Single(db.Recibos.ToList());
        var lineas = db.RecibosLineas.Where(l => l.ReciboId == recibo.Id).OrderBy(l => l.Orden).ToList();
        Assert.Equal(2, lineas.Count); // una línea por ítem del grupo
        Assert.Equal("Cuota mensual", lineas[0].Descripcion);
        Assert.Equal("Aporte extra", lineas[1].Descripcion);
        Assert.Equal(8000m, recibo.Importe); // total = suma de las líneas
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
        db.Empresas.Add(new Empresa { Id = 1, Nombre = "X", RazonSocial = "X", Cuit = "30711111111", CondicionIvaId = 1, CreatedAt = DateTime.Now, Emails = [new EmailEmpresa { Email = "x@x.com", CreatedAt = DateTime.Now }] });
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
    public async Task Anular_ReciboSinCae_Falla()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var empresaId = SeedEmpresa(db);
        var service = BuildService(db, MailOk(), AfipFallaUnaVez());

        // El CAE falla → el recibo queda Pendiente sin CAE.
        await service.EmitirIndividualAsync(empresaId, 1000m, "Cobro", DateTime.Today, 2026, 6, enviarMail: false);
        var pendiente = db.Recibos.Single();
        Assert.True(string.IsNullOrEmpty(pendiente.CAE));

        var anular = await service.AnularReciboAsync(pendiente.Id, enviarMail: false);

        Assert.False(anular.Success);                 // F-10: no se puede anular sin CAE
        Assert.Equal(ReciboEstado.Pendiente, db.Recibos.Single().Estado);
        Assert.Empty(db.NotasDeCredito);
    }

    [Fact]
    public async Task MarcarPagado_ReciboAnulado_Falla()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var empresaId = SeedEmpresa(db);
        var service = BuildService(db, MailOk(), AfipOk());

        await service.EmitirIndividualAsync(empresaId, 1000m, "Cobro", DateTime.Today, 2026, 6, enviarMail: false);
        var recibo = db.Recibos.Single();
        Assert.True((await service.AnularReciboAsync(recibo.Id, enviarMail: false)).Success);

        var pagar = await service.MarcarPagadoAsync(recibo.Id);

        Assert.False(pagar.Success);                  // F-09: no se puede pagar un recibo anulado
        Assert.Equal(ReciboEstado.Anulado, db.Recibos.Single().Estado);
    }

    // ── N-10: red de tests (reenvío, pago, anulación con AFIP caído) ──

    private static IAfipService AfipFalla()
    {
        var afip = Substitute.For<IAfipService>();
        afip.ObtenerCAEAsync(Arg.Any<ComprobanteAfipRequest>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CaeResult>.Fail("AFIP no disponible"));
        return afip;
    }

    [Fact]
    public async Task ReenviarMail_ReciboEmitido_EnviaYPasaAEnviado()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var empresaId = SeedEmpresa(db);
        var service = BuildService(db, MailOk());
        await service.EmitirIndividualAsync(empresaId, 1000m, "Cobro", DateTime.Today, 2026, 6, enviarMail: false);
        var reciboId = db.Recibos.Single().Id;
        Assert.Equal(ReciboEstado.Emitido, db.Recibos.Single().Estado);

        var res = await service.ReenviarMailAsync(reciboId);

        Assert.True(res.Success);
        Assert.Equal(ReciboEstado.Enviado, db.Recibos.Single().Estado);
    }

    [Fact]
    public async Task MarcarPagado_ReciboEnviado_QuedaPagadoConFecha()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var empresaId = SeedEmpresa(db);
        var service = BuildService(db, MailOk());
        await service.EmitirIndividualAsync(empresaId, 1000m, "Cobro", DateTime.Today, 2026, 6, enviarMail: true);
        var reciboId = db.Recibos.Single().Id;

        var res = await service.MarcarPagadoAsync(reciboId);

        Assert.True(res.Success);
        var recibo = db.Recibos.Single();
        Assert.Equal(ReciboEstado.Pagado, recibo.Estado);
        Assert.Equal(DateTime.Today, recibo.FechaPago);
    }

    [Fact]
    public async Task MarcarPagado_ReciboPendiente_Falla()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var empresaId = SeedEmpresa(db);
        await BuildService(db, MailOk(), AfipFalla()).EmitirIndividualAsync(empresaId, 1000m, "Cobro", DateTime.Today, 2026, 6, enviarMail: false);
        var reciboId = db.Recibos.Single().Id;

        var res = await BuildService(db, MailOk()).MarcarPagadoAsync(reciboId);

        Assert.False(res.Success);                    // sin CAE no se puede marcar pagado
        Assert.Equal(ReciboEstado.Pendiente, db.Recibos.Single().Estado);
    }

    [Fact]
    public async Task Anular_FalloAfipEnNc_NoDejaEstadoInconsistente()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var empresaId = SeedEmpresa(db);
        await BuildService(db, MailOk()).EmitirIndividualAsync(empresaId, 1000m, "Cobro", DateTime.Today, 2026, 6, enviarMail: false);
        var reciboId = db.Recibos.Single().Id;

        var res = await BuildService(db, MailOk(), AfipFalla()).AnularReciboAsync(reciboId, enviarMail: false);

        Assert.False(res.Success);                                       // AFIP no autorizó la NC
        Assert.Equal(ReciboEstado.Emitido, db.Recibos.Single().Estado);  // nada quedó a medias
        Assert.Empty(db.NotasDeCredito);
    }

    [Fact]
    public async Task Anular_SinMail_NoEnviaMail()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var empresaId = SeedEmpresa(db);
        var mail = MailOk();
        var service = BuildService(db, mail);
        await service.EmitirIndividualAsync(empresaId, 1000m, "Cobro", DateTime.Today, 2026, 6, enviarMail: false);
        var reciboId = db.Recibos.Single().Id;

        var res = await service.AnularReciboAsync(reciboId, enviarMail: false);

        Assert.True(res.Success);
        Assert.Null(res.Data!.ErrorMail);
        Assert.Equal(ReciboEstado.Anulado, db.Recibos.Single().Estado);
        Assert.Equal(1, db.NotasDeCredito.Count());
        await mail.DidNotReceive().EnviarReciboAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Anular_ConMail_EnviaNotaCredito()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var empresaId = SeedEmpresa(db);
        var mail = MailOk();
        var service = BuildService(db, mail);
        await service.EmitirIndividualAsync(empresaId, 1000m, "Cobro", DateTime.Today, 2026, 6, enviarMail: false);
        var reciboId = db.Recibos.Single().Id;

        var res = await service.AnularReciboAsync(reciboId, enviarMail: true);

        Assert.True(res.Success);
        Assert.Null(res.Data!.ErrorMail);
        var nota = db.NotasDeCredito.Single();
        Assert.Equal(nota.NumeroComprobante, res.Data.NumeroComprobante);   // el resultado informa la NC real
        Assert.Equal(nota.PuntoDeVenta, res.Data.PuntoDeVenta);
        await mail.Received(1).EnviarReciboAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<byte[]>(),
            Arg.Is<string>(n => n.StartsWith("NotaCredito_")), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Anular_MailFalla_DevuelveExitoConErrorMail()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var empresaId = SeedEmpresa(db);
        var mail = Substitute.For<IMailService>();
        mail.EnviarReciboAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<bool>.Fail("SMTP caído"));
        var service = BuildService(db, mail);
        await service.EmitirIndividualAsync(empresaId, 1000m, "Cobro", DateTime.Today, 2026, 6, enviarMail: false);
        var reciboId = db.Recibos.Single().Id;

        var res = await service.AnularReciboAsync(reciboId, enviarMail: true);

        Assert.True(res.Success);                       // la NC quedó autorizada y persistida
        Assert.NotNull(res.Data!.ErrorMail);            // pero el fallo de mail llega a la UI
        Assert.Equal(ReciboEstado.Anulado, db.Recibos.Single().Estado);
        Assert.Equal(1, db.NotasDeCredito.Count());
    }

    [Fact]
    public async Task ReenviarMail_ReciboAnulado_EnviaNotaCredito_SinCambiarEstado()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var empresaId = SeedEmpresa(db);
        var mail = MailOk();
        var service = BuildService(db, mail);
        await service.EmitirIndividualAsync(empresaId, 1000m, "Cobro", DateTime.Today, 2026, 6, enviarMail: false);
        var reciboId = db.Recibos.Single().Id;
        Assert.True((await service.AnularReciboAsync(reciboId, enviarMail: false)).Success);

        var res = await service.ReenviarMailAsync(reciboId);

        Assert.True(res.Success);
        Assert.Equal(ReciboEstado.Anulado, db.Recibos.Single().Estado);  // no pasa a Enviado
        await mail.Received(1).EnviarReciboAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<byte[]>(),
            Arg.Is<string>(n => n.StartsWith("NotaCredito_")), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
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
    public async Task EmitirIndividual_MismoPeriodoDosVeces_CreaDosRecibos()
    {
        // P1-4: N recibos individuales por período están permitidos (cobros extraordinarios).
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var empresaId = SeedEmpresa(db);
        var afip = AfipOk();
        var service = BuildService(db, MailOk(), afip);

        var primera = await service.EmitirIndividualAsync(empresaId, 1000m, "Cobro A", DateTime.Today, 2026, 6, enviarMail: true);
        var segunda = await service.EmitirIndividualAsync(empresaId, 2000m, "Cobro B", DateTime.Today, 2026, 6, enviarMail: true);

        Assert.True(primera.Data!.Exito);
        Assert.True(segunda.Data!.Exito);                        // segundo recibo OK (no bloqueado)
        Assert.Equal(2, db.Recibos.Count());                     // dos recibos independientes
        await afip.Received(2).ObtenerCAEAsync(Arg.Any<ComprobanteAfipRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmitirMasivo_SinMail_QuedaEmitidoYNoEnviaMail()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var grupoId = SeedGrupoConEmpresas(db);
        var mail = MailOk();
        var service = BuildService(db, mail);

        var res = await service.EmitirMasivoAsync(grupoId, 2026, 6, enviarMail: false);

        Assert.All(res.Data!, r => Assert.True(r.Exito));                       // CAE obtenido
        Assert.All(db.Recibos.ToList(), r => Assert.Equal(ReciboEstado.Emitido, r.Estado)); // pero no Enviado
        await mail.DidNotReceive().EnviarReciboAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetEstadoMasivo_AntesYDespuesDeEmitir_ReflejaRecibos()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var grupoId = SeedGrupoConEmpresas(db);
        var service = BuildService(db, MailOk());

        var antes = await service.GetEstadoMasivoAsync(grupoId, 2026, 6);
        Assert.Equal(2, antes.Data!.Count);
        Assert.All(antes.Data!, e => Assert.Null(e.Recibo));                    // todas "No emitido"

        await service.EmitirMasivoAsync(grupoId, 2026, 6, enviarMail: false);

        var despues = await service.GetEstadoMasivoAsync(grupoId, 2026, 6);
        Assert.Equal(2, despues.Data!.Count);
        Assert.All(despues.Data!, e => Assert.NotNull(e.Recibo));
        Assert.All(despues.Data!, e => Assert.False(string.IsNullOrEmpty(e.Recibo!.CAE)));
    }

    [Fact]
    public async Task EnviarMasivo_TrasEmitirSinMail_EnviaYDejaEnviado()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var grupoId = SeedGrupoConEmpresas(db);
        var service = BuildService(db, MailOk());

        await service.EmitirMasivoAsync(grupoId, 2026, 6, enviarMail: false);
        Assert.All(db.Recibos.ToList(), r => Assert.Equal(ReciboEstado.Emitido, r.Estado));

        var envio = await service.EnviarMasivoAsync(grupoId, 2026, 6);

        Assert.Equal(2, envio.Data!.Count);
        Assert.All(envio.Data!, r => Assert.True(r.Exito));
        Assert.All(db.Recibos.ToList(), r => Assert.Equal(ReciboEstado.Enviado, r.Estado));
    }

    // ── P0-1: test con DbContexts separados (replica el registro Transient real de la app) ──

    private static CamaraPortuariaDbContext NuevoContextoCp(SqliteTestDb fixture)
    {
        var options = new DbContextOptionsBuilder<CamaraPortuariaDbContext>()
            .UseSqlite(fixture.Connection).Options;
        var db = new CamaraPortuariaDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static CamaraPortuariaReciboService BuildServiceContextosSeparados(SqliteTestDb fixture, IMailService mail, IAfipService? afip = null)
        => new(
            new CpRepos.ReciboRepository(NuevoContextoCp(fixture), NullLogger<CpRepos.ReciboRepository>.Instance),
            new CpRepos.GrupoFacturacionRepository(NuevoContextoCp(fixture), NullLogger<CpRepos.GrupoFacturacionRepository>.Instance),
            new CpRepos.EmpresaRepository(NuevoContextoCp(fixture), NullLogger<CpRepos.EmpresaRepository>.Instance),
            new CpRepos.NotaDeCreditoRepository(NuevoContextoCp(fixture), NullLogger<CpRepos.NotaDeCreditoRepository>.Instance),
            new CpRepos.ConfiguracionRepository(NuevoContextoCp(fixture)),
            afip ?? AfipOk(),
            new CamaraPortuariaPdfService(new AfipDocumentosService(), new FakeAfipConfigProvider()),
            mail,
            NullLogger<CamaraPortuariaReciboService>.Instance);

    [Fact]
    public async Task EmitirMasivo_ConContextosSeparados_EmiteSinReinsertarEmpresa()
    {
        // Replica el escenario real de la app: cada repositorio usa su propio DbContext (Transient).
        // Antes del fix CP, esto falla porque EF re-inserta la empresa al guardar el recibo.
        using var fx = SqliteTestDb.CreateCamara(out var seedDb);
        var grupoId = SeedGrupoConEmpresas(seedDb);

        var service = BuildServiceContextosSeparados(fx, MailOk());

        var res = await service.EmitirMasivoAsync(grupoId, 2026, 6);

        Assert.True(res.Success);
        Assert.Equal(2, res.Data!.Count);
        Assert.All(res.Data, r => Assert.True(r.Exito));

        // Verificar con un contexto de lectura independiente que no se duplicó la empresa.
        using var verDb = NuevoContextoCp(fx);
        Assert.Equal(2, verDb.Empresas.Count());
        Assert.Equal(2, verDb.Recibos.Count());
    }

    [Fact]
    public async Task EmitirMasivo_ConContextosSeparados_CM_YaSinBug()
    {
        // CM no tiene el bug P0-1; este test debe pasar siempre (gemelo de referencia).
        using var fx = SqliteTestDb.CreateCentro(out var seedDb);
        var ag = new CmAgencia { Nombre = "Ag", RazonSocial = "Ag SA", Cuit = "30700000001", CondicionIvaId = 1, CreatedAt = DateTime.Now, Emails = [new EmailAgencia { Email = "a@x.com", CreatedAt = DateTime.Now }] };
        var barco = new Barco { Nombre = "B", CreatedAt = DateTime.Now };
        seedDb.Agencias.Add(ag);
        seedDb.Barcos.Add(barco);
        seedDb.SaveChanges();
        seedDb.Vouchers.Add(new Voucher { AgenciaId = ag.Id, BarcoId = barco.Id, Numero = 1, Importe = 1000m, Fecha = new DateTime(2026, 6, 10), PeriodoAnio = 2026, PeriodoMes = 6, CreatedAt = DateTime.Now });
        seedDb.SaveChanges();

        CentroMaritimoDbContext NuevoContextoCm()
        {
            var opts = new DbContextOptionsBuilder<CentroMaritimoDbContext>().UseSqlite(fx.Connection).Options;
            var db = new CentroMaritimoDbContext(opts);
            db.Database.EnsureCreated();
            return db;
        }

        var service = new CentroMaritimoReciboService(
            new CmRepos.ReciboRepository(NuevoContextoCm(), NullLogger<CmRepos.ReciboRepository>.Instance),
            new CmRepos.GrupoFacturacionRepository(NuevoContextoCm(), NullLogger<CmRepos.GrupoFacturacionRepository>.Instance),
            new CmRepos.AgenciaRepository(NuevoContextoCm(), NullLogger<CmRepos.AgenciaRepository>.Instance),
            new CmRepos.VoucherRepository(NuevoContextoCm(), NullLogger<CmRepos.VoucherRepository>.Instance),
            new CmRepos.NotaDeCreditoRepository(NuevoContextoCm(), NullLogger<CmRepos.NotaDeCreditoRepository>.Instance),
            new CmRepos.ConfiguracionRepository(NuevoContextoCm()),
            AfipOk(),
            new CentroMaritimoPdfService(new PdfMerger(), new AfipDocumentosService(), new FakeAfipConfigProvider()),
            MailOk(),
            NullLogger<CentroMaritimoReciboService>.Instance);

        var res = await service.CerrarPeriodoAsync(2026, 6);

        Assert.True(res.Success);
        Assert.Single(res.Data!);
        Assert.True(res.Data![0].Exito);

        using var verDb = NuevoContextoCm();
        Assert.Equal(1, verDb.Agencias.Count()); // no se reinsertó
        Assert.Equal(1, verDb.Recibos.Count());
    }

    // ── P1-4 (CP) ──

    [Fact]
    public async Task EmitirIndividual_DosVecesMismoPeriodo_CreaDosRecibos()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var empresaId = SeedEmpresa(db);
        var service = BuildService(db, MailOk());

        var r1 = await service.EmitirIndividualAsync(empresaId, 1000m, "Cobro 1", DateTime.Today, 2026, 6, enviarMail: false);
        var r2 = await service.EmitirIndividualAsync(empresaId, 2000m, "Cobro 2", DateTime.Today, 2026, 6, enviarMail: false);

        Assert.True(r1.Data!.Exito);
        Assert.True(r2.Data!.Exito);        // segundo recibo individual debe crearse OK (N por período)
        Assert.Equal(2, db.Recibos.Count()); // dos recibos distintos
        // N-6: invariante fiscal — el total persistido SIEMPRE es la suma de las líneas.
        Assert.All(db.Recibos.ToList(), r => Assert.Equal(db.RecibosLineas.Where(l => l.ReciboId == r.Id).Sum(l => l.Importe), r.Importe));
    }

    [Fact]
    public async Task EmitirIndividual_ConPendientePrevio_RetomaSinDuplicar()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var empresaId = SeedEmpresa(db);
        var afip = AfipFallaUnaVez();
        var service = BuildService(db, MailOk(), afip);

        // Primera emisión falla en CAE → recibo Pendiente
        var r1 = await service.EmitirIndividualAsync(empresaId, 1000m, "Cobro", DateTime.Today, 2026, 6, enviarMail: false);
        Assert.False(r1.Data!.Exito);
        Assert.Equal(1, db.Recibos.Count());

        // Segunda emisión: encuentra el Pendiente y lo retoma
        var r2 = await service.EmitirIndividualAsync(empresaId, 1000m, "Cobro", DateTime.Today, 2026, 6, enviarMail: false);
        Assert.True(r2.Data!.Exito);         // CAE OK en el reintento
        Assert.Equal(1, db.Recibos.Count()); // no se duplicó
    }

    [Fact]
    public async Task EmitirIndividual_PendientePrevioConContenidoDistinto_CreaReciboNuevo()
    {
        // N-1: un Pendiente con contenido DISTINTO no es un reintento — es otro cobro.
        // El segundo pedido NO debe pisar el snapshot del primero.
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var empresaId = SeedEmpresa(db);
        var service = BuildService(db, MailOk(), AfipFallaUnaVez());

        // Primera emisión falla en CAE → "Papelería" queda Pendiente
        var r1 = await service.EmitirIndividualAsync(empresaId, 5000m, "Papelería", DateTime.Today, 2026, 6, enviarMail: false);
        Assert.False(r1.Data!.Exito);

        // Segunda emisión con OTRO contenido → recibo nuevo (AFIP ya responde OK)
        var r2 = await service.EmitirIndividualAsync(empresaId, 20000m, "Cobro extraordinario", DateTime.Today, 2026, 6, enviarMail: false);
        Assert.True(r2.Data!.Exito);

        Assert.Equal(2, db.Recibos.Count());
        var pendiente = db.Recibos.Single(r => r.Estado == ReciboEstado.Pendiente);
        var emitido = db.Recibos.Single(r => r.Estado == ReciboEstado.Emitido);
        Assert.Equal(5000m, pendiente.Importe);                  // el cobro original sobrevive intacto
        Assert.Equal("Papelería", db.RecibosLineas.Single(l => l.ReciboId == pendiente.Id).Descripcion);
        Assert.Equal(20000m, emitido.Importe);
        Assert.Equal("Cobro extraordinario", db.RecibosLineas.Single(l => l.ReciboId == emitido.Id).Descripcion);
        Assert.False(string.IsNullOrEmpty(emitido.CAE));
    }

    [Fact]
    public async Task EmitirMasivo_ReintentoTrasFalloCae_MantieneLineasMultiItem()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var grupo = new CpGrupo
        {
            Nombre = "Cuota+extras", Importe = 0m, CreatedAt = DateTime.Now,
            Lineas =
            [
                new PuertoBB.Core.Entities.CamaraPortuaria.GrupoFacturacionLinea { Descripcion = "Cuota mensual", Cantidad = 1, PrecioUnitario = 5000m, Importe = 5000m, Orden = 0, CreatedAt = DateTime.Now },
                new PuertoBB.Core.Entities.CamaraPortuaria.GrupoFacturacionLinea { Descripcion = "Aporte extra",  Cantidad = 2, PrecioUnitario = 1500m, Importe = 3000m, Orden = 1, CreatedAt = DateTime.Now },
            ]
        };
        db.Grupos.Add(grupo);
        var emp = new Empresa { Nombre = "Uno", RazonSocial = "Uno SA", Cuit = "30711111111", CondicionIvaId = 1, CreatedAt = DateTime.Now, Emails = [new EmailEmpresa { Email = "u@x.com", CreatedAt = DateTime.Now }] };
        db.Empresas.Add(emp);
        db.SaveChanges();
        db.EmpresasGrupos.Add(new EmpresaGrupo { EmpresaId = emp.Id, GrupoFacturacionId = grupo.Id, CreatedAt = DateTime.Now });
        db.SaveChanges();
        var service = BuildService(db, MailOk(), AfipFallaUnaVez());

        var primera = await service.EmitirMasivoAsync(grupo.Id, 2026, 6, enviarMail: false);   // CAE falla → Pendiente
        Assert.All(primera.Data!, r => Assert.False(r.Exito));
        Assert.Equal(ReciboEstado.Pendiente, db.Recibos.Single().Estado);

        var segunda = await service.EmitirMasivoAsync(grupo.Id, 2026, 6, enviarMail: false);    // resume vía GetPorClave → CAE OK
        Assert.All(segunda.Data!, r => Assert.True(r.Exito));

        var recibo = db.Recibos.Single();
        Assert.Equal(ReciboEstado.Emitido, recibo.Estado);
        var lineas = db.RecibosLineas.Where(l => l.ReciboId == recibo.Id).OrderBy(l => l.Orden).ToList();
        Assert.Equal(2, lineas.Count);                            // los ítems del grupo se mantienen tras el reintento
        Assert.Equal("Cuota mensual", lineas[0].Descripcion);
        Assert.Equal(8000m, recibo.Importe);                      // total = suma de las líneas
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

    private static CentroMaritimoReciboService BuildService(CentroMaritimoDbContext db, IMailService mail, IAfipService? afip = null, ICentroMaritimoPdfService? pdf = null)
        => new(
            new CmRepos.ReciboRepository(db, NullLogger<CmRepos.ReciboRepository>.Instance),
            new CmRepos.GrupoFacturacionRepository(db, NullLogger<CmRepos.GrupoFacturacionRepository>.Instance),
            new CmRepos.AgenciaRepository(db, NullLogger<CmRepos.AgenciaRepository>.Instance),
            new CmRepos.VoucherRepository(db, NullLogger<CmRepos.VoucherRepository>.Instance),
            new CmRepos.NotaDeCreditoRepository(db, NullLogger<CmRepos.NotaDeCreditoRepository>.Instance),
            new CmRepos.ConfiguracionRepository(db),
            afip ?? AfipOk(),
            pdf ?? new CentroMaritimoPdfService(new PdfMerger(), new AfipDocumentosService(), new FakeAfipConfigProvider()),
            mail,
            NullLogger<CentroMaritimoReciboService>.Instance);

    [Fact]
    public async Task EmitirIndividual_AgenciaSinCondicionIva_FallaSinLlamarAfip_CM()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        var ag = new CmAgencia { Nombre = "SinCond", RazonSocial = "SinCond SA", Cuit = "30733333334", CreatedAt = DateTime.Now, Emails = [new EmailAgencia { Email = "s@x.com", CreatedAt = DateTime.Now }] };
        db.Agencias.Add(ag);
        db.SaveChanges();
        var afip = AfipOk();
        var service = BuildService(db, MailOk(), afip);

        var res = await service.EmitirIndividualAsync(ag.Id, 1000m, "Cobro", DateTime.Today, 2026, 6, enviarMail: false);

        Assert.False(res.Data!.Exito);
        Assert.Contains("RG 5616", res.Data.ErrorEmision);
        await afip.DidNotReceive().ObtenerCAEAsync(Arg.Any<ComprobanteAfipRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CerrarPeriodo_ConsolidaVouchers_EnUnReciboPorAgencia()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        var ag = new CmAgencia { Nombre = "Ag", RazonSocial = "Ag SA", Cuit = "30700000001", CondicionIvaId = 1, CreatedAt = DateTime.Now, Emails = [new EmailAgencia { Email = "a@x.com", CreatedAt = DateTime.Now }] };
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
        // N-6: invariante fiscal — el total persistido SIEMPRE es la suma de las líneas.
        Assert.Equal(db.RecibosLineas.Where(l => l.ReciboId == recibo.Id).Sum(l => l.Importe), recibo.Importe);
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

    // ── P1-1 ──

    private static IAfipService AfipFallaUnaVezCm()
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

    private static (CmAgencia agencia, Barco barco) SeedAgenciaConVouchers(CentroMaritimoDbContext db, int cantVouchers)
    {
        var ag = new CmAgencia { Nombre = "Ag", RazonSocial = "Ag SA", Cuit = "30700000001", CondicionIvaId = 1, CreatedAt = DateTime.Now, Emails = [new EmailAgencia { Email = "a@x.com", CreatedAt = DateTime.Now }] };
        var barco = new Barco { Nombre = "B", CreatedAt = DateTime.Now };
        db.Agencias.Add(ag);
        db.Barcos.Add(barco);
        db.SaveChanges();
        for (int i = 1; i <= cantVouchers; i++)
            db.Vouchers.Add(new Voucher { AgenciaId = ag.Id, BarcoId = barco.Id, Numero = i, Importe = 1000m, Fecha = new DateTime(2026, 6, i), PeriodoAnio = 2026, PeriodoMes = 6, CreatedAt = DateTime.Now });
        db.SaveChanges();
        return (ag, barco);
    }

    [Fact]
    public async Task CerrarPeriodo_FallaCae_PersisteReciboPendienteYVouchersVinculados()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        SeedAgenciaConVouchers(db, cantVouchers: 2);
        var service = BuildService(db, MailOk(), AfipFallaUnaVezCm());

        var res = await service.CerrarPeriodoAsync(2026, 6);

        Assert.True(res.Success);
        var r = Assert.Single(res.Data!);
        Assert.False(r.Exito);  // CAE falló

        var recibo = db.Recibos.Single();
        Assert.Equal(ReciboEstado.Pendiente, recibo.Estado);
        Assert.True(string.IsNullOrEmpty(recibo.CAE));
        Assert.All(db.Vouchers.ToList(), v => Assert.Equal(recibo.Id, v.ReciboId));
    }

    [Fact]
    public async Task CerrarPeriodo_ReintentoTrasFalloCae_CompletaSinDuplicar()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        SeedAgenciaConVouchers(db, cantVouchers: 2);
        var service = BuildService(db, MailOk(), AfipFallaUnaVezCm());

        await service.CerrarPeriodoAsync(2026, 6);  // primer intento: CAE falla
        Assert.Equal(1, db.Recibos.Count());        // persiste Pendiente

        var service2 = BuildService(db, MailOk());  // segundo servicio con AFIP OK
        var res2 = await service2.CerrarPeriodoAsync(2026, 6);

        Assert.True(res2.Success);
        var r = Assert.Single(res2.Data!);
        Assert.True(r.Exito);

        Assert.Equal(1, db.Recibos.Count());  // no se duplicó
        var recibo = db.Recibos.Single();
        Assert.False(string.IsNullOrEmpty(recibo.CAE));
        Assert.NotEqual(ReciboEstado.Pendiente, recibo.Estado);
    }

    // ── N-3: reintento de consolidado con vouchers nuevos, con DbContexts separados (Transient real) ──

    private static CentroMaritimoDbContext NuevoContextoCm(SqliteTestDb fx)
    {
        var opts = new DbContextOptionsBuilder<CentroMaritimoDbContext>().UseSqlite(fx.Connection).Options;
        var db = new CentroMaritimoDbContext(opts);
        db.Database.EnsureCreated();
        return db;
    }

    private static CentroMaritimoReciboService BuildServiceContextosSeparados(
        SqliteTestDb fx, IMailService mail, IAfipService? afip = null, ICentroMaritimoPdfService? pdf = null)
        => new(
            new CmRepos.ReciboRepository(NuevoContextoCm(fx), NullLogger<CmRepos.ReciboRepository>.Instance),
            new CmRepos.GrupoFacturacionRepository(NuevoContextoCm(fx), NullLogger<CmRepos.GrupoFacturacionRepository>.Instance),
            new CmRepos.AgenciaRepository(NuevoContextoCm(fx), NullLogger<CmRepos.AgenciaRepository>.Instance),
            new CmRepos.VoucherRepository(NuevoContextoCm(fx), NullLogger<CmRepos.VoucherRepository>.Instance),
            new CmRepos.NotaDeCreditoRepository(NuevoContextoCm(fx), NullLogger<CmRepos.NotaDeCreditoRepository>.Instance),
            new CmRepos.ConfiguracionRepository(NuevoContextoCm(fx)),
            afip ?? AfipOk(),
            pdf ?? new CentroMaritimoPdfService(new PdfMerger(), new AfipDocumentosService(), new FakeAfipConfigProvider()),
            mail,
            NullLogger<CentroMaritimoReciboService>.Instance);

    [Fact]
    public async Task CerrarPeriodo_ReintentoConVoucherNuevo_ConContextosSeparados_IncluyeTodosLosVouchers()
    {
        // N-3: los vouchers nuevos se vinculan en OTRO DbContext (Transient); el mail y el count
        // del resultado deben verlos igual. Con contexto compartido este test pasaría siempre.
        using var fx = SqliteTestDb.CreateCentro(out var seedDb);
        var (ag, barco) = SeedAgenciaConVouchers(seedDb, cantVouchers: 1);   // V1 = $1000

        // 1er cierre: CAE falla → consolidado Pendiente con V1
        var service1 = BuildServiceContextosSeparados(fx, MailOk(), AfipFallaUnaVezCm());
        var primera = await service1.CerrarPeriodoAsync(2026, 6);
        Assert.False(primera.Data![0].Exito);

        // Aparece un voucher nuevo del mismo período
        seedDb.Vouchers.Add(new Voucher { AgenciaId = ag.Id, BarcoId = barco.Id, Numero = 2, Importe = 500m, Fecha = new DateTime(2026, 6, 20), PeriodoAnio = 2026, PeriodoMes = 6, CreatedAt = DateTime.Now });
        seedDb.SaveChanges();

        // 2do cierre: AFIP OK; el PDF fake captura qué vouchers viajan en el mail
        List<long> vouchersEnviados = [];
        var pdf = Substitute.For<ICentroMaritimoPdfService>();
        pdf.GenerarPdfDescargaAsync(Arg.Any<IReadOnlyList<Voucher>>(), Arg.Any<CmRecibo?>(), Arg.Any<CancellationToken>())
            .Returns(ci => { vouchersEnviados = ci.Arg<IReadOnlyList<Voucher>>().Select(v => (long)v.Numero).ToList(); return Task.FromResult(new byte[] { 1 }); });
        var service2 = BuildServiceContextosSeparados(fx, MailOk(), AfipOk(), pdf);

        var segunda = await service2.CerrarPeriodoAsync(2026, 6);

        var r = Assert.Single(segunda.Data!);
        Assert.True(r.Exito);
        Assert.Equal(2, r.CantidadVouchers);                     // count incluye el voucher nuevo
        Assert.Equal(1500m, r.Importe);
        Assert.Equal(2, vouchersEnviados.Count);                 // el PDF del mail incluye AMBOS vouchers
        using var verDb = NuevoContextoCm(fx);
        var recibo = verDb.Recibos.Single(x => x.Estado != ReciboEstado.Anulado);
        Assert.Equal(1500m, recibo.Importe);
        Assert.Equal(2, verDb.RecibosLineas.Count(l => l.ReciboId == recibo.Id));
    }

    // ── Reintento de "Emitir recibos" (sin mail) tras fallo de CAE ──

    [Fact]
    public async Task EmitirRecibosPeriodo_ReintentoTrasFalloCae_ProcesaConsolidadoPendiente()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        SeedAgenciaConVouchers(db, cantVouchers: 2);
        var mail = MailOk();
        var service = BuildService(db, mail, AfipFallaUnaVezCm());

        var primera = await service.EmitirRecibosPeriodoAsync(2026, 6);
        Assert.True(primera.Success);
        var r1 = Assert.Single(primera.Data!);
        Assert.False(r1.Exito);                                  // CAE falló
        Assert.NotNull(r1.ErrorEmision);                         // el motivo viaja al llamador
        Assert.Equal(ReciboEstado.Pendiente, db.Recibos.Single().Estado);

        // Los vouchers ya quedaron vinculados; el consolidado Pendiente debe reintentarse igual.
        var segunda = await service.EmitirRecibosPeriodoAsync(2026, 6);
        var r2 = Assert.Single(segunda.Data!);
        Assert.True(r2.Exito);
        var recibo = db.Recibos.Single();                        // no se duplicó
        Assert.Equal(ReciboEstado.Emitido, recibo.Estado);       // emitido pero sin mail
        Assert.False(string.IsNullOrEmpty(recibo.CAE));
        await mail.DidNotReceive().EnviarReciboAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmitirReciboAgencia_ReintentoTrasFalloCae_SinVouchersLibres_Emite()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        var (ag, _) = SeedAgenciaConVouchers(db, cantVouchers: 2);
        var service = BuildService(db, MailOk(), AfipFallaUnaVezCm());

        var primera = await service.EmitirReciboAgenciaAsync(ag.Id, 2026, 6);
        Assert.False(primera.Success);                           // CAE falló → error con motivo
        Assert.Equal(ReciboEstado.Pendiente, db.Recibos.Single().Estado);

        var segunda = await service.EmitirReciboAgenciaAsync(ag.Id, 2026, 6);
        Assert.True(segunda.Success);                            // reintento sobre el consolidado Pendiente
        Assert.True(segunda.Data!.Exito);
        Assert.Equal(1, db.Recibos.Count());
        Assert.Equal(ReciboEstado.Emitido, db.Recibos.Single().Estado);
    }

    [Fact]
    public async Task EmitirReciboAgencia_SinVouchersNiConsolidadoPendiente_Falla()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        var (ag, _) = SeedAgenciaConVouchers(db, cantVouchers: 1);
        var service = BuildService(db, MailOk());

        var emision = await service.EmitirReciboAgenciaAsync(ag.Id, 2026, 6);
        Assert.True(emision.Success);                            // emite OK (queda Emitido, con CAE)

        var repetida = await service.EmitirReciboAgenciaAsync(ag.Id, 2026, 6);
        Assert.False(repetida.Success);                          // ya no hay nada para emitir
        Assert.Contains("no tiene vouchers pendientes", repetida.ErrorMessage);
        Assert.Equal(1, db.Recibos.Count());
    }

    [Fact]
    public async Task EmitirRecibos_SinPuntoDeVentaActivo_FallaConMensajeClaro()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        var (ag, _) = SeedAgenciaConVouchers(db, cantVouchers: 1);
        db.PuntosDeVenta.Single().Activo = false;                // sin punto de venta activo
        db.SaveChanges();
        var service = BuildService(db, MailOk());

        var masivo = await service.EmitirRecibosPeriodoAsync(2026, 6);
        Assert.False(masivo.Success);
        Assert.Contains("punto de venta", masivo.ErrorMessage);

        var porAgencia = await service.EmitirReciboAgenciaAsync(ag.Id, 2026, 6);
        Assert.False(porAgencia.Success);
        Assert.Contains("punto de venta", porAgencia.ErrorMessage);

        Assert.Empty(db.Recibos);                                // no se persistió nada a medias
    }

    // ── P1-2 (CM) ──

    [Fact]
    public async Task Anular_PersisteReciboYNotaJuntos_CM()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        var (ag, _) = SeedAgenciaConVouchers(db, cantVouchers: 1);
        var service = BuildService(db, MailOk());

        await service.CerrarPeriodoAsync(2026, 6);
        var reciboId = db.Recibos.Single().Id;
        var anulacion = await service.AnularReciboAsync(reciboId, enviarMail: false);

        Assert.True(anulacion.Success);
        Assert.Equal(ReciboEstado.Anulado, db.Recibos.Single().Estado);
        Assert.Equal(1, db.NotasDeCredito.Count());
    }

    private static CmAgencia SeedAgenciaSola(CentroMaritimoDbContext db)
    {
        var ag = new CmAgencia { Nombre = "Ag", RazonSocial = "Ag SA", Cuit = "30700000001", CondicionIvaId = 1, CreatedAt = DateTime.Now, Emails = [new EmailAgencia { Email = "a@x.com", CreatedAt = DateTime.Now }] };
        db.Agencias.Add(ag);
        db.SaveChanges();
        return ag;
    }

    [Fact]
    public async Task Anular_SinMail_NoEnviaMail_CM()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        var ag = SeedAgenciaSola(db);
        var mail = MailOk();
        var service = BuildService(db, mail);
        await service.EmitirIndividualAsync(ag.Id, 1000m, "Cobro", DateTime.Today, 2026, 6, enviarMail: false);
        var reciboId = db.Recibos.Single().Id;

        var res = await service.AnularReciboAsync(reciboId, enviarMail: false);

        Assert.True(res.Success);
        Assert.Null(res.Data!.ErrorMail);
        Assert.Equal(ReciboEstado.Anulado, db.Recibos.Single().Estado);
        Assert.Equal(1, db.NotasDeCredito.Count());
        await mail.DidNotReceive().EnviarReciboAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Anular_ConMail_EnviaNotaCredito_CM()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        var ag = SeedAgenciaSola(db);
        var mail = MailOk();
        var service = BuildService(db, mail);
        await service.EmitirIndividualAsync(ag.Id, 1000m, "Cobro", DateTime.Today, 2026, 6, enviarMail: false);
        var reciboId = db.Recibos.Single().Id;

        var res = await service.AnularReciboAsync(reciboId, enviarMail: true);

        Assert.True(res.Success);
        Assert.Null(res.Data!.ErrorMail);
        var nota = db.NotasDeCredito.Single();
        Assert.Equal(nota.NumeroComprobante, res.Data.NumeroComprobante);   // el resultado informa la NC real
        Assert.Equal(nota.PuntoDeVenta, res.Data.PuntoDeVenta);
        await mail.Received(1).EnviarReciboAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<byte[]>(),
            Arg.Is<string>(n => n.StartsWith("NotaCredito_")), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Anular_MailFalla_DevuelveExitoConErrorMail_CM()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        var ag = SeedAgenciaSola(db);
        var mail = Substitute.For<IMailService>();
        mail.EnviarReciboAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<bool>.Fail("SMTP caído"));
        var service = BuildService(db, mail);
        await service.EmitirIndividualAsync(ag.Id, 1000m, "Cobro", DateTime.Today, 2026, 6, enviarMail: false);
        var reciboId = db.Recibos.Single().Id;

        var res = await service.AnularReciboAsync(reciboId, enviarMail: true);

        Assert.True(res.Success);                       // la NC quedó autorizada y persistida
        Assert.NotNull(res.Data!.ErrorMail);            // pero el fallo de mail llega a la UI
        Assert.Equal(ReciboEstado.Anulado, db.Recibos.Single().Estado);
        Assert.Equal(1, db.NotasDeCredito.Count());
    }

    [Fact]
    public async Task ReenviarMail_ReciboAnulado_EnviaNotaCredito_SinCambiarEstado_CM()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        var ag = SeedAgenciaSola(db);
        var mail = MailOk();
        var service = BuildService(db, mail);
        await service.EmitirIndividualAsync(ag.Id, 1000m, "Cobro", DateTime.Today, 2026, 6, enviarMail: false);
        var reciboId = db.Recibos.Single().Id;
        Assert.True((await service.AnularReciboAsync(reciboId, enviarMail: false)).Success);

        var res = await service.ReenviarMailAsync(reciboId);

        Assert.True(res.Success);
        Assert.Equal(ReciboEstado.Anulado, db.Recibos.Single().Estado);  // no pasa a Enviado
        await mail.Received(1).EnviarReciboAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<byte[]>(),
            Arg.Is<string>(n => n.StartsWith("NotaCredito_")), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── P1-3 ──

    [Fact]
    public async Task AnularConsolidado_PermiteReemitirElPeriodo()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        var (ag, barco) = SeedAgenciaConVouchers(db, cantVouchers: 2);
        var service = BuildService(db, MailOk());

        // Cerrar período → consolidado emitido
        var cierre1 = await service.CerrarPeriodoAsync(2026, 6);
        Assert.True(cierre1.Data![0].Exito);
        var reciboId = db.Recibos.Single().Id;

        // Anular el consolidado → vouchers liberados
        var anulacion = await service.AnularReciboAsync(reciboId, enviarMail: false);
        Assert.True(anulacion.Success);
        Assert.All(db.Vouchers.ToList(), v => Assert.Null(v.ReciboId)); // P1-3: vouchers liberados

        // Agregar un voucher nuevo y volver a cerrar el mismo período
        db.Vouchers.Add(new Voucher { AgenciaId = ag.Id, BarcoId = barco.Id, Numero = 3, Importe = 500m, Fecha = new DateTime(2026, 6, 15), PeriodoAnio = 2026, PeriodoMes = 6, CreatedAt = DateTime.Now });
        db.SaveChanges();

        var cierre2 = await service.CerrarPeriodoAsync(2026, 6);
        Assert.True(cierre2.Data![0].Exito);  // debe emitir sin error de índice único
        Assert.Equal(2, db.Recibos.Count());   // el anulado + el nuevo
        var nuevo = db.Recibos.Single(r => r.Estado != ReciboEstado.Anulado);
        Assert.Equal(3, nuevo.Vouchers.Count); // 3 vouchers en el nuevo consolidado
    }

    // ── P1-4 (CM) ──

    [Fact]
    public async Task EmitirIndividual_DosVecesMismoPeriodo_CreaDosRecibos_CM()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        var ag = new CmAgencia { Nombre = "Ag", RazonSocial = "Ag SA", Cuit = "30700000001", CondicionIvaId = 1, CreatedAt = DateTime.Now, Emails = [new EmailAgencia { Email = "a@x.com", CreatedAt = DateTime.Now }] };
        db.Agencias.Add(ag);
        db.SaveChanges();
        var service = BuildService(db, MailOk());

        var r1 = await service.EmitirIndividualAsync(ag.Id, 1000m, "Cobro 1", DateTime.Today, 2026, 6, enviarMail: false);
        var r2 = await service.EmitirIndividualAsync(ag.Id, 2000m, "Cobro 2", DateTime.Today, 2026, 6, enviarMail: false);

        Assert.True(r1.Data!.Exito);
        Assert.True(r2.Data!.Exito);        // segundo recibo individual debe crearse OK
        Assert.Equal(2, db.Recibos.Count()); // dos recibos distintos, no duplicados
        // N-6: invariante fiscal — el total persistido SIEMPRE es la suma de las líneas.
        Assert.All(db.Recibos.ToList(), r => Assert.Equal(db.RecibosLineas.Where(l => l.ReciboId == r.Id).Sum(l => l.Importe), r.Importe));
    }

    [Fact]
    public async Task EmitirIndividual_PendientePrevioConContenidoDistinto_CreaReciboNuevo_CM()
    {
        // N-1 (gemelo CM): el Pendiente con contenido distinto no se pisa.
        using var fx = SqliteTestDb.CreateCentro(out var db);
        var ag = new CmAgencia { Nombre = "Ag", RazonSocial = "Ag SA", Cuit = "30700000001", CondicionIvaId = 1, CreatedAt = DateTime.Now, Emails = [new EmailAgencia { Email = "a@x.com", CreatedAt = DateTime.Now }] };
        db.Agencias.Add(ag);
        db.SaveChanges();
        var service = BuildService(db, MailOk(), AfipFallaUnaVezCm());

        var r1 = await service.EmitirIndividualAsync(ag.Id, 5000m, "Papelería", DateTime.Today, 2026, 6, enviarMail: false);
        Assert.False(r1.Data!.Exito);                            // CAE falló → Pendiente

        var r2 = await service.EmitirIndividualAsync(ag.Id, 20000m, "Cobro extraordinario", DateTime.Today, 2026, 6, enviarMail: false);
        Assert.True(r2.Data!.Exito);

        Assert.Equal(2, db.Recibos.Count());
        var pendiente = db.Recibos.Single(r => r.Estado == ReciboEstado.Pendiente);
        Assert.Equal(5000m, pendiente.Importe);                  // el cobro original sobrevive intacto
        Assert.Equal("Papelería", db.RecibosLineas.Single(l => l.ReciboId == pendiente.Id).Descripcion);
        var emitido = db.Recibos.Single(r => r.Estado == ReciboEstado.Emitido);
        Assert.Equal(20000m, emitido.Importe);
    }

    // ── N-10: red de tests CM (reenvío de consolidado, pago, grupo, anulación con AFIP caído,
    //          y variantes con DbContexts separados) ──

    private static IAfipService AfipFallaCm()
    {
        var afip = Substitute.For<IAfipService>();
        afip.ObtenerCAEAsync(Arg.Any<ComprobanteAfipRequest>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CaeResult>.Fail("AFIP no disponible"));
        return afip;
    }

    [Fact]
    public async Task ReenviarMail_Consolidado_UsaPdfDescargaConTodosLosVouchers()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        SeedAgenciaConVouchers(db, cantVouchers: 2);
        await BuildService(db, MailOk()).EmitirRecibosPeriodoAsync(2026, 6);   // Emitido, sin mail
        var reciboId = db.Recibos.Single().Id;

        List<long> vouchersEnviados = [];
        var pdf = Substitute.For<ICentroMaritimoPdfService>();
        pdf.GenerarPdfDescargaAsync(Arg.Any<IReadOnlyList<Voucher>>(), Arg.Any<CmRecibo?>(), Arg.Any<CancellationToken>())
            .Returns(ci => { vouchersEnviados = ci.Arg<IReadOnlyList<Voucher>>().Select(v => (long)v.Numero).ToList(); return Task.FromResult(new byte[] { 1 }); });
        var service = BuildService(db, MailOk(), pdf: pdf);

        var res = await service.ReenviarMailAsync(reciboId);

        Assert.True(res.Success);
        Assert.Equal(2, vouchersEnviados.Count);                        // PDF único con TODOS los vouchers
        Assert.Equal(ReciboEstado.Enviado, db.Recibos.Single().Estado);
    }

    [Fact]
    public async Task MarcarPagado_ReciboPendiente_Falla_CM()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        SeedAgenciaConVouchers(db, cantVouchers: 1);
        await BuildService(db, MailOk(), AfipFallaCm()).CerrarPeriodoAsync(2026, 6);  // queda Pendiente
        var reciboId = db.Recibos.Single().Id;

        var res = await BuildService(db, MailOk()).MarcarPagadoAsync(reciboId);

        Assert.False(res.Success);                    // sin CAE no se puede marcar pagado
        Assert.Equal(ReciboEstado.Pendiente, db.Recibos.Single().Estado);
    }

    [Fact]
    public async Task EmitirDeGrupo_AgenciaDelGrupo_EmiteConLineasDelGrupo()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        var ag = new CmAgencia { Nombre = "Ag", RazonSocial = "Ag SA", Cuit = "30700000001", CondicionIvaId = 1, CreatedAt = DateTime.Now, Emails = [new EmailAgencia { Email = "a@x.com", CreatedAt = DateTime.Now }] };
        var grupo = new PuertoBB.Core.Entities.CentroMaritimo.GrupoFacturacion { Nombre = "Cuota social", Importe = 4000m, CreatedAt = DateTime.Now };
        db.Agencias.Add(ag);
        db.Grupos.Add(grupo);
        db.SaveChanges();
        db.AgenciasGrupos.Add(new AgenciaGrupo { AgenciaId = ag.Id, GrupoFacturacionId = grupo.Id, CreatedAt = DateTime.Now });
        db.SaveChanges();
        var service = BuildService(db, MailOk());

        var res = await service.EmitirDeGrupoAsync(grupo.Id, ag.Id, 2026, 6, enviarMail: false);

        Assert.True(res.Success);
        Assert.True(res.Data!.Exito);
        var recibo = db.Recibos.Single();
        Assert.Equal(4000m, recibo.Importe);
        Assert.Equal(1, db.EmisionesGrupo.Count());                     // vínculo grupo-recibo creado
        Assert.Equal("Cuota social", db.RecibosLineas.Single(l => l.ReciboId == recibo.Id).Descripcion);
    }

    [Fact]
    public async Task Anular_FalloAfipEnNc_NoDejaEstadoInconsistente_CM()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        SeedAgenciaConVouchers(db, cantVouchers: 2);
        await BuildService(db, MailOk()).CerrarPeriodoAsync(2026, 6);   // consolidado emitido
        var reciboId = db.Recibos.Single().Id;

        var res = await BuildService(db, MailOk(), AfipFallaCm()).AnularReciboAsync(reciboId, enviarMail: false);

        Assert.False(res.Success);                                      // AFIP no autorizó la NC
        var recibo = db.Recibos.Single();
        Assert.NotEqual(ReciboEstado.Anulado, recibo.Estado);           // nada quedó a medias
        Assert.Empty(db.NotasDeCredito);
        Assert.All(db.Vouchers.ToList(), v => Assert.Equal(recibo.Id, v.ReciboId));   // siguen vinculados
    }

    [Fact]
    public async Task AnularConsolidado_ConContextosSeparados_DesvinculaVouchers()
    {
        using var fx = SqliteTestDb.CreateCentro(out var seedDb);
        SeedAgenciaConVouchers(seedDb, cantVouchers: 2);
        var service = BuildServiceContextosSeparados(fx, MailOk());

        await service.CerrarPeriodoAsync(2026, 6);
        int reciboId;
        using (var lectura = NuevoContextoCm(fx))
            reciboId = lectura.Recibos.Single().Id;

        var anulacion = await service.AnularReciboAsync(reciboId, enviarMail: false);
        Assert.True(anulacion.Success);

        using var verDb = NuevoContextoCm(fx);
        Assert.Equal(ReciboEstado.Anulado, verDb.Recibos.Single().Estado);
        Assert.All(verDb.Vouchers.ToList(), v => Assert.Null(v.ReciboId));  // P1-3 con Transient real
        Assert.Equal(1, verDb.NotasDeCredito.Count());
    }

    [Fact]
    public async Task EmitirIndividual_ConContextosSeparados_CM()
    {
        using var fx = SqliteTestDb.CreateCentro(out var seedDb);
        var ag = new CmAgencia { Nombre = "Ag", RazonSocial = "Ag SA", Cuit = "30700000001", CondicionIvaId = 1, CreatedAt = DateTime.Now, Emails = [new EmailAgencia { Email = "a@x.com", CreatedAt = DateTime.Now }] };
        seedDb.Agencias.Add(ag);
        seedDb.SaveChanges();
        var service = BuildServiceContextosSeparados(fx, MailOk());

        var res = await service.EmitirIndividualAsync(ag.Id, 800m, "Cobro extra", DateTime.Today, 2026, 6, enviarMail: true);

        Assert.True(res.Data!.Exito);
        using var verDb = NuevoContextoCm(fx);
        Assert.Equal(1, verDb.Agencias.Count());                        // no se reinsertó la agencia
        var recibo = verDb.Recibos.Single();
        Assert.Equal(ReciboEstado.Enviado, recibo.Estado);
        Assert.False(string.IsNullOrEmpty(recibo.CAE));
    }

    [Fact]
    public async Task EmitirIndividual_EnPeriodoConsolidado_NoChocaConElConsolidado_CM()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        var (ag, _) = SeedAgenciaConVouchers(db, cantVouchers: 1);
        var service = BuildService(db, MailOk());

        await service.CerrarPeriodoAsync(2026, 6);  // consolidado emitido

        var ind = await service.EmitirIndividualAsync(ag.Id, 500m, "Cobro extra", DateTime.Today, 2026, 6, enviarMail: false);
        Assert.True(ind.Data!.Exito);           // individual no choca con el consolidado
        Assert.Equal(2, db.Recibos.Count());    // 1 consolidado + 1 individual
    }
}

public class VoucherServiceTests
{
    [Fact]
    public async Task CrearVoucher_AsignaNumeroYDerivaPeriodo()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        var ag = new CmAgencia { Nombre = "Ag", RazonSocial = "Ag", Cuit = "30700000001", CondicionIvaId = 1, CreatedAt = DateTime.Now };
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
        var ag1 = new CmAgencia { Nombre = "Norte",  RazonSocial = "N",  Cuit = "30700000001", CondicionIvaId = 1, CreatedAt = DateTime.Now };
        var ag2 = new CmAgencia { Nombre = "Centro", RazonSocial = "C",  Cuit = "30700000002", CondicionIvaId = 1, CreatedAt = DateTime.Now };
        var ag3 = new CmAgencia { Nombre = "Sur",    RazonSocial = "S",  Cuit = "30700000003", CondicionIvaId = 1, CreatedAt = DateTime.Now };
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

    [Fact]
    public async Task GetDelPeriodo_DevuelveConsolidadosYPendientes_ConRecibo()
    {
        using var fx = SqliteTestDb.CreateCentro(out var db);
        var ag = new CmAgencia { Nombre = "Ag", RazonSocial = "Ag", Cuit = "30700000001", CondicionIvaId = 1, CreatedAt = DateTime.Now };
        var barco = new Barco { Nombre = "B", CreatedAt = DateTime.Now };
        db.Agencias.Add(ag);
        db.Barcos.Add(barco);
        db.SaveChanges();

        var reciboEmitido = new CmRecibo
        {
            AgenciaId = ag.Id, PeriodoAnio = 2026, PeriodoMes = 6,
            Importe = 3000m, EsConsolidadoVouchers = true,
            PuntoDeVenta = 1, TipoComprobante = TipoComprobante.Recibo, CodigoAfip = 211,
            NumeroComprobante = 101, CAE = "12345678901234",
            FechaVencimientoCAE = DateTime.Today.AddDays(10),
            FechaEmision = DateTime.Today, FechaVencimientoPago = DateTime.Today.AddDays(30),
            Estado = ReciboEstado.Emitido, CreatedAt = DateTime.Now
        };
        db.Recibos.Add(reciboEmitido);
        db.SaveChanges();

        db.Vouchers.AddRange(
            new Voucher { AgenciaId = ag.Id, BarcoId = barco.Id, Numero = 1, Importe = 1000m, Fecha = new DateTime(2026, 6, 5), PeriodoAnio = 2026, PeriodoMes = 6, ReciboId = reciboEmitido.Id, CreatedAt = DateTime.Now },
            new Voucher { AgenciaId = ag.Id, BarcoId = barco.Id, Numero = 2, Importe = 2000m, Fecha = new DateTime(2026, 6, 8), PeriodoAnio = 2026, PeriodoMes = 6, ReciboId = reciboEmitido.Id, CreatedAt = DateTime.Now },
            new Voucher { AgenciaId = ag.Id, BarcoId = barco.Id, Numero = 3, Importe = 500m,  Fecha = new DateTime(2026, 6, 11), PeriodoAnio = 2026, PeriodoMes = 6, CreatedAt = DateTime.Now });
        db.SaveChanges();

        var service = new VoucherService(
            new CmRepos.VoucherRepository(db, NullLogger<CmRepos.VoucherRepository>.Instance),
            new CmRepos.ContadorVoucherRepository(db),
            new CmRepos.AgenciaRepository(db, NullLogger<CmRepos.AgenciaRepository>.Instance),
            new CmRepos.BarcoRepository(db, NullLogger<CmRepos.BarcoRepository>.Instance),
            NullLogger<VoucherService>.Instance);

        var res = await service.GetDelPeriodoAsync(2026, 6);

        Assert.True(res.Success);
        var lista = res.Data!.ToList();
        // El consolidado NO desaparece: vienen los 3 (2 consolidados + 1 pendiente), ordenados por Numero.
        Assert.Equal(new[] { 1, 2, 3 }, lista.Select(v => v.Numero).ToArray());
        // Los consolidados traen su recibo cargado para poder mostrar el estado.
        Assert.Equal(ReciboEstado.Emitido, lista.Single(v => v.Numero == 1).Recibo!.Estado);
        Assert.Null(lista.Single(v => v.Numero == 3).ReciboId);
    }
}
