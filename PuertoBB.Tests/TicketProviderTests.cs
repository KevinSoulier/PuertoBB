using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Afip;
using Afip.Abstractions;
using Afip.Wsaa;
using Xunit;

namespace PuertoBB.Tests;

public class TicketProviderTests
{
    private const string Cuit = "20111111112";

    // WSAA falso: devuelve un loginTicketResponse válido sin tocar la red.
    private sealed class FakeWsaaClient : IWsaaClient
    {
        public Task<string> LoginCmsAsync(string cmsFirmadoBase64, bool usarHomologacion, CancellationToken ct = default)
            => Task.FromResult(
                "<loginTicketResponse><credentials><token>TOKEN-OK</token><sign>SIGN-OK</sign></credentials>" +
                "<header><expirationTime>2099-01-01T00:00:00</expirationTime></header></loginTicketResponse>");
    }

    private static TicketProvider NuevoProvider()
        => new(new FakeWsaaClient(), new TicketCache(new InMemoryTicketStore()));

    [Fact]
    public async Task GetTicketAsync_SinCertificado_LanzaMensajeDeNoConfigurado()
    {
        var options = new AfipOptions { Cuit = Cuit, UsarHomologacion = true };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NuevoProvider().GetTicketAsync("wsfe", options));
        Assert.Contains("No hay certificado configurado", ex.Message);
    }

    [Fact]
    public async Task GetTicketAsync_ConCertificadoEnMemoria_NoLanzaPorFaltaDeCertificadoYDevuelveTicket()
    {
        var (crt, key) = GenerarCrtKey();
        var options = new AfipOptions
        {
            Cuit = Cuit,
            UsarHomologacion = true,
            CertificadoContenido = crt,
            CertificadoKeyContenido = key,
        };

        // Con el certificado en memoria (sin CertificadoRuta) ya no debe fallar la validación previa:
        // firma el TRA, llama al WSAA falso y obtiene el ticket.
        var ticket = await NuevoProvider().GetTicketAsync("wsfe", options);

        Assert.Equal("TOKEN-OK", ticket.Token);
        Assert.Equal("SIGN-OK", ticket.Sign);
    }

    // Genera una clave + certificado autofirmado en memoria (sustituye al par .crt/.key real de AFIP).
    private static (byte[] crt, byte[] key) GenerarCrtKey()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest($"CN=puertobb, SERIALNUMBER=CUIT {Cuit}", rsa,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));
        var crtPem = Encoding.UTF8.GetBytes(cert.ExportCertificatePem());
        var keyPem = Encoding.UTF8.GetBytes(rsa.ExportPkcs8PrivateKeyPem());
        return (crtPem, keyPem);
    }
}
