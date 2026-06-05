using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;

namespace PuertoBB.Services.Afip;

/// <summary>
/// Genera el TRA (Ticket de Requerimiento de Acceso) y lo firma como CMS PKCS#7 (SHA1+RSA).
/// El resultado en Base64 se pasa a loginCms() del WSAA.
/// </summary>
public static class TraBuilder
{
    public static string GenerarTraXml(string servicio = "wsfe")
    {
        var ahora = DateTimeOffset.Now;
        var tra = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("loginTicketRequest",
                new XAttribute("version", "1.0"),
                new XElement("header",
                    new XElement("uniqueId", Random.Shared.Next(1, int.MaxValue)),
                    new XElement("generationTime", ahora.AddMinutes(-10).ToString("yyyy-MM-ddTHH:mm:sszzz")),
                    new XElement("expirationTime", ahora.AddMinutes(10).ToString("yyyy-MM-ddTHH:mm:sszzz"))),
                new XElement("service", servicio)));

        return tra.Declaration + Environment.NewLine + tra.ToString(SaveOptions.DisableFormatting);
    }

    public static string FirmarCms(string traXml, string certificadoRuta, string? certificadoPassword)
    {
        var cert = X509CertificateLoader.LoadPkcs12FromFile(
            certificadoRuta,
            certificadoPassword,
            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

        var traBytes = Encoding.UTF8.GetBytes(traXml);
        var signedCms = new SignedCms(new ContentInfo(traBytes), detached: false);
        var signer = new CmsSigner(cert) { IncludeOption = X509IncludeOption.EndCertOnly };
        signedCms.ComputeSignature(signer);

        return Convert.ToBase64String(signedCms.Encode());
    }
}
