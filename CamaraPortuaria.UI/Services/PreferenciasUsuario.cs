using System.IO;

namespace CamaraPortuaria.UI.Services;

/// <summary>Persistencia simple de preferencias de UI (tema) en un archivo de texto en AppData.</summary>
public static class PreferenciasUsuario
{
    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Puerto de Bahia Blanca", "CamaraPortuaria");

    private static string TemaPath => Path.Combine(Dir, "tema.txt");

    /// <summary>Devuelve "Light" | "Dark" | "System" (default System).</summary>
    public static string GetTema()
    {
        try { return File.Exists(TemaPath) ? File.ReadAllText(TemaPath).Trim() : "System"; }
        catch { return "System"; }
    }

    public static void SetTema(string tema)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(TemaPath, tema);
        }
        catch { /* preferencia no crítica */ }
    }
}
