using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Afip.Wsaa;
using Xunit;

namespace PuertoBB.Tests;

public class CsrGeneratorTests
{
    private const string Cuit = "20111111112";
    private const string RazonSocial = "MI EMPRESA SA";
    private const string Alias = "puertobb";

    [Fact]
    public void Generar_DevuelveCsrPemValidoConCuitYCn()
    {
        var res = CsrGenerator.Generar(Cuit, RazonSocial, Alias);

        var csrPem = Encoding.UTF8.GetString(res.CsrPem);
        Assert.Contains("-----BEGIN CERTIFICATE REQUEST-----", csrPem);

        // El CSR se puede re-parsear y el subject contiene CN, O y el CUIT en serialNumber.
        var req = CertificateRequest.LoadSigningRequestPem(csrPem, HashAlgorithmName.SHA256);
        var subject = req.SubjectName.Name;
        Assert.Contains($"CN={Alias}", subject);
        Assert.Contains(RazonSocial, subject);
        Assert.Contains($"CUIT {Cuit}", subject);
    }

    [Fact]
    public void Generar_LaClaveEmparejaConElCsr_YPermiteCrearCertificadoDesdePem()
    {
        var res = CsrGenerator.Generar(Cuit, RazonSocial, Alias);
        var keyPem = Encoding.UTF8.GetString(res.ClavePrivadaPem);
        Assert.Contains("-----BEGIN PRIVATE KEY-----", keyPem); // PKCS#8

        // Simula el .crt que devolvería AFIP: un cert autofirmado con la misma clave.
        var crtPem = EmitirCertificadoDesde(res);

        // Modo CRT+KEY: el par (.crt, .key) debe cargar como X509 con clave privada (igual que TraBuilder).
        using var cert = X509Certificate2.CreateFromPem(crtPem, keyPem);
        Assert.True(cert.HasPrivateKey);
    }

    [Fact]
    public void ArmarP12_ProduceUnPkcs12CargableConElPassword()
    {
        var res = CsrGenerator.Generar(Cuit, RazonSocial, Alias);
        var crtPem = EmitirCertificadoDesde(res);
        const string password = "clave-secreta";

        var p12 = CsrGenerator.ArmarP12(Encoding.UTF8.GetBytes(crtPem), res.ClavePrivadaPem, password);

        using var cargado = X509CertificateLoader.LoadPkcs12(p12, password, X509KeyStorageFlags.EphemeralKeySet);
        Assert.True(cargado.HasPrivateKey);
    }

    [Theory]
    [InlineData("", RazonSocial, Alias)]
    [InlineData(Cuit, "", Alias)]
    [InlineData(Cuit, RazonSocial, "")]
    public void Generar_FaltanDatos_Lanza(string cuit, string razon, string alias)
        => Assert.Throws<ArgumentException>(() => CsrGenerator.Generar(cuit, razon, alias));

    [Theory]
    [InlineData("puertobb")]
    [InlineData("camara-prod")]
    [InlineData("centro_homo")]
    [InlineData("ABC123")]
    public void ValidarAlias_Validos_DevuelveNull(string alias)
        => Assert.Null(CsrGenerator.ValidarAlias(alias));

    [Theory]
    [InlineData("")]                       // vacío
    [InlineData("   ")]                     // solo espacios
    [InlineData("puerto bb")]              // espacio
    [InlineData("puértobb")]              // acento
    [InlineData("puerto/bb")]             // caracter inválido
    [InlineData("puerto.bb")]             // punto
    public void ValidarAlias_Invalidos_DevuelveMensaje(string alias)
        => Assert.NotNull(CsrGenerator.ValidarAlias(alias));

    [Fact]
    public void ValidarAlias_MasDe64_DevuelveMensaje()
        => Assert.NotNull(CsrGenerator.ValidarAlias(new string('a', 65)));

    [Fact]
    public void Generar_AliasInvalido_Lanza()
        => Assert.Throws<ArgumentException>(() => CsrGenerator.Generar(Cuit, RazonSocial, "alias con espacios"));

    // Reconstruye el CSR y autofirma un certificado con su clave (sustituye al .crt que daría AFIP).
    private static string EmitirCertificadoDesde(CsrGenerator.Resultado res)
    {
        var keyPem = Encoding.UTF8.GetString(res.ClavePrivadaPem);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(keyPem);
        var req = new CertificateRequest(
            $"CN={Alias}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));
        return cert.ExportCertificatePem();
    }
}
