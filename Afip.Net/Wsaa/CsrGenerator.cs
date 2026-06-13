using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace Afip.Wsaa;

/// <summary>
/// Genera, dentro de la app, la clave privada y el CSR (Certificate Signing Request) que hay que
/// subir a AFIP para tramitar un certificado — sin necesidad de OpenSSL. AFIP devuelve un .crt que
/// se usa junto con la clave en modo CRT+KEY (ver <see cref="TraBuilder"/>). También arma un .p12
/// para reutilizar el certificado en otras aplicaciones.
/// </summary>
public static class CsrGenerator
{
    /// <summary>CSR y clave privada generados, ambos en formato PEM (UTF-8).</summary>
    /// <param name="CsrPem">Pedido de certificado (.csr) a subir a AFIP.</param>
    /// <param name="ClavePrivadaPem">Clave privada (.key) en PKCS#8; conservarla para usar el .crt que devuelva AFIP.</param>
    public sealed record Resultado(byte[] CsrPem, byte[] ClavePrivadaPem);

    /// <summary>Caracteres y longitud admitidos para el alias (CN): letras, dígitos, "-" y "_", hasta 64.</summary>
    private static readonly Regex AliasValido = new("^[A-Za-z0-9_-]+$", RegexOptions.Compiled);

    /// <summary>
    /// Valida el alias (CN). Devuelve <c>null</c> si es válido o un mensaje de error para mostrar.
    /// El alias se usa como Common Name del certificado y conviene que coincida con el del portal de
    /// AFIP: alfanumérico, sin espacios ni acentos, hasta 64 caracteres (límite del X.509).
    /// </summary>
    public static string? ValidarAlias(string? alias)
    {
        var a = (alias ?? "").Trim();
        if (a.Length == 0)
            return "Indicá un alias (CN) para el certificado.";
        if (a.Length > 64)
            return "El alias (CN) no puede superar los 64 caracteres.";
        if (!AliasValido.IsMatch(a))
            return "El alias (CN) solo puede tener letras, números, guiones (-) y guiones bajos (_), sin espacios ni acentos.";
        return null;
    }

    /// <summary>
    /// Genera una clave RSA de 2048 bits y un CSR firmado con SHA-256. El subject sigue el formato
    /// que pide AFIP: <c>C=AR, O=&lt;razónSocial&gt;, CN=&lt;alias&gt;, serialNumber=CUIT &lt;cuit&gt;</c>.
    /// </summary>
    /// <param name="cuit">CUIT del emisor, solo dígitos (sin guiones).</param>
    /// <param name="razonSocial">Razón social del emisor (organización).</param>
    /// <param name="alias">Alias/nombre común del certificado (CN), ej. "puertobb".</param>
    public static Resultado Generar(string cuit, string razonSocial, string alias)
    {
        var digitos = new string((cuit ?? "").Where(char.IsDigit).ToArray());
        if (digitos.Length == 0)
            throw new ArgumentException("El CUIT es obligatorio para generar el certificado.", nameof(cuit));
        if (string.IsNullOrWhiteSpace(razonSocial))
            throw new ArgumentException("La razón social es obligatoria para generar el certificado.", nameof(razonSocial));
        if (ValidarAlias(alias) is { } errorAlias)
            throw new ArgumentException(errorAlias, nameof(alias));

        var subject = new X500DistinguishedNameBuilder();
        subject.AddCountryOrRegion("AR");
        subject.AddOrganizationName(razonSocial.Trim());
        subject.AddCommonName(alias.Trim());
        subject.Add("2.5.4.5", $"CUIT {digitos}"); // OID serialNumber

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            subject.Build(), rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var csrPem = request.CreateSigningRequestPem();
        var keyPem = rsa.ExportPkcs8PrivateKeyPem();

        return new Resultado(Encoding.UTF8.GetBytes(csrPem), Encoding.UTF8.GetBytes(keyPem));
    }

    /// <summary>
    /// Arma un PKCS#12 (.p12) con contraseña a partir del certificado (.crt PEM) que devolvió AFIP y
    /// la clave privada (.key PEM) generada en <see cref="Generar"/>, para reutilizarlo en otras apps.
    /// </summary>
    public static byte[] ArmarP12(byte[] crtPem, byte[] clavePrivadaPem, string password)
    {
        if (crtPem is not { Length: > 0 })
            throw new ArgumentException("Falta el certificado (.crt) para armar el .p12.", nameof(crtPem));
        if (clavePrivadaPem is not { Length: > 0 })
            throw new ArgumentException("Falta la clave privada para armar el .p12.", nameof(clavePrivadaPem));

        using var cert = X509Certificate2.CreateFromPem(
            Encoding.UTF8.GetString(crtPem), Encoding.UTF8.GetString(clavePrivadaPem));
        return cert.Export(X509ContentType.Pkcs12, password);
    }
}
