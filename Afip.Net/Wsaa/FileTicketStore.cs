using System.Text.Json;

namespace Afip.Wsaa;

/// <summary>
/// Almacén de tickets persistido a disco como JSON (texto plano). Sobrevive a reinicios de la app y
/// así evita re-loguear en WSAA en cada arranque (AFIP penaliza los logins frecuentes con
/// "El CEE ya posee un TA válido").
/// </summary>
public sealed class FileTicketStore : ITicketStore
{
    private readonly string _directorio;

    public FileTicketStore(string directorio)
    {
        _directorio = directorio;
        Directory.CreateDirectory(_directorio);
        Barrer();   // limpieza única al arrancar: descarta TA vencidos de sesiones anteriores
    }

    public AfipTicket? Load(string clave)
    {
        var path = Ruta(clave);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<AfipTicket>(File.ReadAllText(path));
        }
        catch
        {
            // Ticket corrupto o ilegible (o de un formato previo): se ignora y se renovará.
            return null;
        }
    }

    public void Save(string clave, AfipTicket ticket)
    {
        File.WriteAllText(Ruta(clave), JsonSerializer.Serialize(ticket));
        Barrer();   // tras renovar, descarta cualquier TA que haya quedado vencido (p. ej. de un certificado anterior)
    }

    /// <summary>
    /// Borra del directorio los tickets vencidos o ilegibles. Con la clave indexada por credenciales,
    /// rotar el certificado deja archivos huérfanos: estos vencen en ~12 hs y este barrido los elimina,
    /// de modo que la carpeta no crece sin límite. No necesita conocer el formato de la clave.
    /// </summary>
    private void Barrer()
    {
        foreach (var path in Directory.EnumerateFiles(_directorio, "ta-*.json"))
        {
            try
            {
                var ticket = JsonSerializer.Deserialize<AfipTicket>(File.ReadAllText(path));
                if (ticket is null || ticket.Expiration <= DateTime.Now)
                    File.Delete(path);
            }
            catch
            {
                // Corrupto, de un formato previo o tomado por otro proceso: intentar borrar e ignorar errores.
                try { File.Delete(path); } catch { /* otro proceso lo tiene: se reintentará en el próximo barrido */ }
            }
        }
    }

    private string Ruta(string clave)
    {
        var invalidos = Path.GetInvalidFileNameChars();
        var safe = string.Concat(clave.Select(c => invalidos.Contains(c) ? '_' : c));
        return Path.Combine(_directorio, $"ta-{safe}.json");
    }
}
