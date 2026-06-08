namespace Afip.Abstractions;

/// <summary>
/// Cliente del WebService de Autenticación (WSAA). El método loginCms recibe el CMS firmado
/// (Base64) y devuelve el XML del Ticket de Acceso (loginTicketResponse).
/// La implementación concreta se genera desde el WSDL con dotnet-svcutil (ver afip-integracion.md).
/// </summary>
public interface IWsaaClient
{
    Task<string> LoginCmsAsync(string cmsFirmadoBase64, bool usarHomologacion, CancellationToken ct = default);
}
