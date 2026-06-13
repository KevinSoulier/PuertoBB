using PuertoBB.Core.Models.Mail;
using Xunit;

namespace PuertoBB.Tests;

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
}
