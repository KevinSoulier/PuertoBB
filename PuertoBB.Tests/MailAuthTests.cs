using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Mail;
using PuertoBB.Services.Mail;
using PuertoBB.Tests.TestSupport;
using Xunit;
using CpRepos = PuertoBB.Infrastructure.Repositories.CamaraPortuaria;
using CpEntities = PuertoBB.Core.Entities.CamaraPortuaria;

namespace PuertoBB.Tests;

public class CuentaCorreoRepoTests
{
    [Fact]
    public async Task Seed_TienePrincipalActiva()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var repo = new CpRepos.ConfiguracionRepository(db);

        var cuentas = await repo.GetCuentasCorreoAsync();

        var activa = Assert.Single(cuentas, c => c.Activo);
        Assert.Equal("Principal", activa.Nombre);
    }

    [Fact]
    public async Task MarcarActiva_DejaExactamenteUnaActiva()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var repo = new CpRepos.ConfiguracionRepository(db);
        var nueva = await repo.GuardarCuentaCorreoAsync(new CuentaCorreo { Nombre = "Ventas", SmtpPort = 587, Autenticacion = 2 });

        await repo.MarcarCuentaCorreoActivaAsync(nueva.Id);

        var cuentas = await repo.GetCuentasCorreoAsync();
        Assert.Equal(2, cuentas.Count);
        Assert.Equal(nueva.Id, Assert.Single(cuentas, c => c.Activo).Id);
    }

    [Fact]
    public async Task Eliminar_QuitaLaCuenta()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var repo = new CpRepos.ConfiguracionRepository(db);
        var nueva = await repo.GuardarCuentaCorreoAsync(new CuentaCorreo { Nombre = "Temp", SmtpPort = 587 });

        await repo.EliminarCuentaCorreoAsync(nueva.Id);

        Assert.DoesNotContain(await repo.GetCuentasCorreoAsync(), c => c.Id == nueva.Id);
    }

    [Fact]
    public async Task CuentaActiva_SeReflejaEnConfiguracion()
    {
        using var fx = SqliteTestDb.CreateCamara(out var db);
        var repo = new CpRepos.ConfiguracionRepository(db);
        var nueva = await repo.GuardarCuentaCorreoAsync(new CuentaCorreo { Nombre = "Admin", SmtpHost = "smtp.x.com", SmtpPort = 587 });
        await repo.MarcarCuentaCorreoActivaAsync(nueva.Id);

        var config = await repo.GetSinTrackingAsync();

        Assert.NotNull(config.CuentaCorreoActiva);
        Assert.Equal("Admin", config.CuentaCorreoActiva!.Nombre);
        Assert.Equal("smtp.x.com", config.CuentaCorreoActiva.SmtpHost);
    }
}

public class OAuthPresetsTests
{
    [Fact]
    public void Microsoft_Interactivo_UsaCommonYScopeOffline()
    {
        var cfg = new MailConfig { OAuthProveedor = OAuthProveedor.Microsoft, OAuthFlujo = OAuthFlujo.Interactivo };
        var ep = OAuthPresets.Resolver(cfg);

        Assert.Contains("login.microsoftonline.com/common/oauth2/v2.0/authorize", ep.AuthorizeEndpoint);
        Assert.Contains("login.microsoftonline.com/common/oauth2/v2.0/token", ep.TokenEndpoint);
        // outlook.office.com sirve para cuentas personales y de empresa (office365.com lo rechazan las personales).
        Assert.Contains("https://outlook.office.com/SMTP.Send", ep.Scope);
        Assert.Contains("offline_access", ep.Scope);
    }

    [Fact]
    public void OutlookPersonal_CompartirEndpointsMicrosoftYScopeOfficeCom()
    {
        var cfg = new MailConfig { OAuthProveedor = OAuthProveedor.OutlookPersonal, OAuthFlujo = OAuthFlujo.Interactivo };
        var ep = OAuthPresets.Resolver(cfg);

        Assert.Contains("login.microsoftonline.com/common/oauth2/v2.0/authorize", ep.AuthorizeEndpoint);
        Assert.Contains("https://outlook.office.com/SMTP.Send", ep.Scope);
    }

    [Fact]
    public void OutlookPersonal_SugiereHostOutlookCom()
    {
        var s = OAuthPresets.SugerenciaSmtp(OAuthProveedor.OutlookPersonal);
        Assert.NotNull(s);
        Assert.Equal("smtp-mail.outlook.com", s!.Value.Host);
        Assert.Equal(587, s.Value.Puerto);
    }

    [Fact]
    public void Microsoft_Cliente_UsaTenantConfiguradoYScopeDefault()
    {
        var cfg = new MailConfig { OAuthProveedor = OAuthProveedor.Microsoft, OAuthFlujo = OAuthFlujo.Cliente, OAuthTenantId = "tenant-123" };
        var ep = OAuthPresets.Resolver(cfg);

        Assert.Contains("/tenant-123/oauth2/v2.0/token", ep.TokenEndpoint);
        Assert.Equal("https://outlook.office365.com/.default", ep.Scope);
    }

    [Fact]
    public void Google_EndpointsYScopeDeGmail()
    {
        var cfg = new MailConfig { OAuthProveedor = OAuthProveedor.Google, OAuthFlujo = OAuthFlujo.Interactivo };
        var ep = OAuthPresets.Resolver(cfg);

        Assert.Equal("https://accounts.google.com/o/oauth2/v2/auth", ep.AuthorizeEndpoint);
        Assert.Equal("https://oauth2.googleapis.com/token", ep.TokenEndpoint);
        Assert.Contains("https://mail.google.com/", ep.Scope);
    }

    [Fact]
    public void Personalizado_RespetaLoConfigurado()
    {
        var cfg = new MailConfig
        {
            OAuthProveedor = OAuthProveedor.Personalizado,
            OAuthAuthorizeEndpoint = "https://idp.example/authorize",
            OAuthTokenEndpoint = "https://idp.example/token",
            OAuthScope = "smtp.send"
        };
        var ep = OAuthPresets.Resolver(cfg);

        Assert.Equal("https://idp.example/authorize", ep.AuthorizeEndpoint);
        Assert.Equal("https://idp.example/token", ep.TokenEndpoint);
        Assert.Equal("smtp.send", ep.Scope);
    }

    [Fact]
    public void ScopeManual_TienePrioridadSobreElDelProveedor()
    {
        var cfg = new MailConfig { OAuthProveedor = OAuthProveedor.Microsoft, OAuthFlujo = OAuthFlujo.Cliente, OAuthScope = "scope-custom" };
        Assert.Equal("scope-custom", OAuthPresets.Resolver(cfg).Scope);
    }

    [Theory]
    [InlineData(OAuthProveedor.Microsoft, "smtp.office365.com")]
    [InlineData(OAuthProveedor.Google, "smtp.gmail.com")]
    public void SugerenciaSmtp_HostPorProveedor(OAuthProveedor proveedor, string host)
    {
        var s = OAuthPresets.SugerenciaSmtp(proveedor);
        Assert.NotNull(s);
        Assert.Equal(host, s!.Value.Host);
        Assert.Equal(587, s.Value.Puerto);
    }

    [Fact]
    public void SugerenciaSmtp_Personalizado_EsNull() =>
        Assert.Null(OAuthPresets.SugerenciaSmtp(OAuthProveedor.Personalizado));
}

public class MailConfigValidarTests
{
    private static MailConfig Base(MailAutenticacion auth) => new()
    {
        SmtpHost = "smtp.x.com", EmailRemitente = "info@x.com", Autenticacion = auth
    };

    [Fact]
    public void SinServidor_Falla() =>
        Assert.NotNull(new MailConfig { EmailRemitente = "a@b.com" }.Validar());

    [Fact]
    public void SinRemitente_Falla() =>
        Assert.NotNull(new MailConfig { SmtpHost = "smtp.x.com" }.Validar());

    [Fact]
    public void Ninguna_NoExigeCredenciales() =>
        Assert.Null(Base(MailAutenticacion.Ninguna).Validar());

    [Fact]
    public void Basica_SinUsuario_Falla() =>
        Assert.NotNull(Base(MailAutenticacion.Basica).Validar());

    [Fact]
    public void Basica_ConUsuario_Ok() =>
        Assert.Null((Base(MailAutenticacion.Basica) with { SmtpUsuario = "info@x.com" }).Validar());

    [Fact]
    public void OAuth2_SinClientId_Falla() =>
        Assert.NotNull(Base(MailAutenticacion.OAuth2).Validar());

    [Fact]
    public void OAuth2_Interactivo_SinRefreshToken_Falla() =>
        Assert.NotNull((Base(MailAutenticacion.OAuth2) with { OAuthClientId = "cid", OAuthFlujo = OAuthFlujo.Interactivo }).Validar());

    [Fact]
    public void OAuth2_Interactivo_ConRefreshToken_Ok() =>
        Assert.Null((Base(MailAutenticacion.OAuth2) with
        {
            OAuthClientId = "cid", OAuthFlujo = OAuthFlujo.Interactivo, OAuthRefreshToken = "rt"
        }).Validar());

    [Fact]
    public void OAuth2_Cliente_SinSecret_Falla() =>
        Assert.NotNull((Base(MailAutenticacion.OAuth2) with
        {
            OAuthClientId = "cid", OAuthFlujo = OAuthFlujo.Cliente, OAuthTenantId = "t"
        }).Validar());

    [Fact]
    public void OAuth2_Cliente_Microsoft_SinTenant_Falla() =>
        Assert.NotNull((Base(MailAutenticacion.OAuth2) with
        {
            OAuthClientId = "cid", OAuthFlujo = OAuthFlujo.Cliente, OAuthClientSecret = "sec",
            OAuthProveedor = OAuthProveedor.Microsoft
        }).Validar());

    [Fact]
    public void OAuth2_Cliente_Completo_Ok() =>
        Assert.Null((Base(MailAutenticacion.OAuth2) with
        {
            OAuthClientId = "cid", OAuthFlujo = OAuthFlujo.Cliente, OAuthClientSecret = "sec", OAuthTenantId = "t"
        }).Validar());
}

public class MailErroresTests
{
    [Theory]
    [InlineData("535: 5.7.139 Authentication unsuccessful, basic authentication is disabled.")]
    [InlineData("5.7.139 algo")]
    [InlineData("Basic authentication is disabled for this mailbox")]
    public void BasicAuthDeshabilitada_SugiereOAuth2(string mensaje) =>
        Assert.Contains("OAuth2", MailErrores.Describir(mensaje));

    [Fact]
    public void OtroError_SeDevuelveSinCambios()
    {
        const string m = "No route to host";
        Assert.Equal(m, MailErrores.Describir(m));
    }

    [Fact]
    public void Timeout_NoCanceladoPorUsuario_SugiereReintentar() =>
        Assert.Contains("tiempo de espera",
            MailErrores.Describir(new TaskCanceledException(), canceladoPorUsuario: false));

    [Fact]
    public void Cancelacion_DelUsuario_NoEsMensajeDeTimeout() =>
        Assert.DoesNotContain("tiempo de espera",
            MailErrores.Describir(new TaskCanceledException(), canceladoPorUsuario: true));
}

public class MailServiceProbarConexionTests
{
    // QW2: la sobrecarga que recibe un MailConfig debe probar ESE config (la cuenta en edición),
    // sin consultar al provider (la cuenta activa de la base).
    [Fact]
    public async Task ProbarConexionConConfig_UsaElConfigRecibido_NoConsultaElActivo()
    {
        var provider = Substitute.For<IMailConfigProvider>();
        provider.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new MailConfig { SmtpHost = "smtp.activa.com", EmailRemitente = "activa@x.com" });
        var svc = new MailService(provider, Substitute.For<IMailTokenProvider>(), NullLogger<MailService>.Instance);

        // Config en edición sin servidor → falla por validación local antes de tocar la red.
        var res = await svc.ProbarConexionAsync(new MailConfig { EmailRemitente = "edit@x.com" });

        Assert.False(res.Success);
        await provider.DidNotReceive().GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fake_ProbarConexionConConfig_Ok()
    {
        var fake = new FakeMailService(NullLogger<FakeMailService>.Instance);
        var res = await fake.ProbarConexionAsync(new MailConfig { SmtpHost = "smtp.x.com", EmailRemitente = "a@b.com" });
        Assert.True(res.Success);
    }
}
