using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Afip.Wsaa;

/// <summary>
/// Almacén de tickets persistido a disco y cifrado con DPAPI (Windows, scope CurrentUser).
/// Sobrevive a reinicios de la app y así evita re-loguear en WSAA en cada arranque
/// (AFIP penaliza los logins frecuentes con "El CEE ya posee un TA válido").
/// En SO no-Windows se degrada de forma transparente a almacenamiento en memoria.
/// </summary>
public sealed class FileTicketStore : ITicketStore
{
    private readonly string _directorio;
    private readonly InMemoryTicketStore _fallback = new();

    public FileTicketStore(string directorio)
    {
        _directorio = directorio;
        Directory.CreateDirectory(_directorio);
    }

    public AfipTicket? Load(string clave)
    {
        if (!OperatingSystem.IsWindows()) return _fallback.Load(clave);

        var path = Ruta(clave);
        if (!File.Exists(path)) return null;
        try
        {
            var cifrado = File.ReadAllBytes(path);
            var bytes = ProtectedData.Unprotect(cifrado, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<AfipTicket>(Encoding.UTF8.GetString(bytes));
        }
        catch
        {
            // Ticket corrupto o ilegible (p. ej. otro usuario): se ignora y se renovará.
            return null;
        }
    }

    public void Save(string clave, AfipTicket ticket)
    {
        if (!OperatingSystem.IsWindows()) { _fallback.Save(clave, ticket); return; }

        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(ticket));
        var cifrado = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(Ruta(clave), cifrado);
    }

    private string Ruta(string clave)
    {
        var invalidos = Path.GetInvalidFileNameChars();
        var safe = string.Concat(clave.Select(c => invalidos.Contains(c) ? '_' : c));
        return Path.Combine(_directorio, $"ta-{safe}.bin");
    }
}
