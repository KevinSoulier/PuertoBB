using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;

namespace Afip.Wsaa;

/// <summary>
/// Genera el TRA (Ticket de Requerimiento de Acceso) y lo firma como CMS PKCS#7 (SHA1+RSA).
/// El resultado en Base64 se pasa a loginCms() del WSAA.
/// </summary>
public static class TraBuilder
{
    // uniqueId monótono y único por proceso (evita colisiones de TRA generados en el mismo segundo).
    private static long _uniqueId = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public static string GenerarTraXml(string servicio = "wsfe")
    {
        var ahora = DateTimeOffset.Now;
        var tra = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("loginTicketRequest",
                new XAttribute("version", "1.0"),
                new XElement("header",
                    new XElement("uniqueId", Interlocked.Increment(ref _uniqueId) % int.MaxValue),
                    new XElement("generationTime", ahora.AddMinutes(-10).ToString("yyyy-MM-ddTHH:mm:sszzz")),
                    new XElement("expirationTime", ahora.AddMinutes(10).ToString("yyyy-MM-ddTHH:mm:sszzz"))),
                new XElement("service", servicio)));

        return tra.Declaration + Environment.NewLine + tra.ToString(SaveOptions.DisableFormatting);
    }

    public static string FirmarCms(string traXml, AfipOptions options)
    {
        // El contenido en memoria tiene prioridad; si solo hay ruta, se lee el archivo.
        var certBytes = options.CertificadoContenido ?? LeerArchivo(options.CertificadoRuta)
            ?? throw new InvalidOperationException("No se configuró el certificado (ni contenido ni ruta).");
        var keyBytes = options.CertificadoKeyContenido ?? LeerArchivo(options.CertificadoKeyRuta);

        // Con clave privada presente → modo CRT+KEY (PEM); si no → P12.
        var cert = keyBytes is not null
            ? X509Certificate2.CreateFromPem(Encoding.UTF8.GetString(certBytes), Encoding.UTF8.GetString(keyBytes))
            : X509CertificateLoader.LoadPkcs12(certBytes, options.CertificadoPassword, X509KeyStorageFlags.EphemeralKeySet);

        var traBytes = Encoding.UTF8.GetBytes(traXml);
        var signedCms = new SignedCms(new ContentInfo(traBytes), detached: false);
        var signer = new CmsSigner(cert) { IncludeOption = X509IncludeOption.EndCertOnly };
        signedCms.ComputeSignature(signer);

        return Convert.ToBase64String(signedCms.Encode());

        static byte[]? LeerArchivo(string? ruta) =>
            string.IsNullOrEmpty(ruta) ? null : File.ReadAllBytes(ruta);
    }
}
