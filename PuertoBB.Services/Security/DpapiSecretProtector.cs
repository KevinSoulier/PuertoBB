using System.Security.Cryptography;
using System.Text;

namespace PuertoBB.Services.Security;

/// <summary>
/// Protege secretos en reposo con DPAPI (Windows, scope CurrentUser). El valor protegido se guarda
/// como "dpapi:" + Base64. Si el valor almacenado no tiene ese prefijo se trata como texto plano
/// (migración suave de datos guardados antes de activar el cifrado). En SO no-Windows no cifra.
/// </summary>
public sealed class DpapiSecretProtector : ISecretProtector
{
    private const string Prefijo = "dpapi:";

    public string? Protect(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext) || !OperatingSystem.IsWindows())
            return plaintext;

        var protegido = ProtectedData.Protect(Encoding.UTF8.GetBytes(plaintext), optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Prefijo + Convert.ToBase64String(protegido);
    }

    public string? Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored) || !stored.StartsWith(Prefijo, StringComparison.Ordinal))
            return stored; // vacío o texto plano legado

        if (!OperatingSystem.IsWindows())
            return stored;

        try
        {
            var bytes = Convert.FromBase64String(stored[Prefijo.Length..]);
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser));
        }
        catch
        {
            return null; // no se pudo descifrar (otro usuario/máquina): se pedirá recargar la contraseña
        }
    }
}
