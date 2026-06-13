using System.Globalization;

namespace PuertoBB.Services.Common;

/// <summary>Formateo común (moneda, período) en cultura es-AR.</summary>
public static class Formato
{
    private static readonly CultureInfo EsAr = CultureInfo.GetCultureInfo("es-AR");

    public static string Moneda(decimal importe) => importe.ToString("C2", EsAr);

    public static string Periodo(int anio, int mes)
    {
        var nombre = EsAr.DateTimeFormat.GetMonthName(mes);
        return $"{char.ToUpper(nombre[0])}{nombre[1..]} {anio}";
    }

    public static string Fecha(DateTime fecha) => fecha.ToString("dd/MM/yyyy", EsAr);

    /// <summary>Comprobante AFIP formateado: "0001-00000094".</summary>
    public static string Comprobante(int puntoDeVenta, long numero) => $"{puntoDeVenta:0000}-{numero:00000000}";

    public static string Cuit(string cuit)
        => cuit.Length == 11 ? $"{cuit[..2]}-{cuit[2..10]}-{cuit[10..]}" : cuit;

    /// <summary>Extrae solo dígitos de un CUIT y lo devuelve como long. Devuelve 0 si está vacío o es inválido.</summary>
    public static long ParseCuit(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        var digits = new string(s.Where(char.IsDigit).ToArray());
        return long.TryParse(digits, out var v) ? v : 0;
    }

    /// <summary>Devuelve (80, cuit_long) si hay dígitos, o (99, 0) para Consumidor Final.</summary>
    public static (int tipo, long nro) ParseReceptorDoc(string? cuit)
    {
        var nro = ParseCuit(cuit);
        return nro == 0 ? (99, 0) : (80, nro);
    }

    /// <summary>
    /// Convierte un texto en un nombre de archivo seguro (sin extensión): reemplaza los
    /// caracteres inválidos por espacio, colapsa espacios repetidos y recorta. Si queda vacío
    /// devuelve "comprobante".
    /// </summary>
    public static string NombreArchivoSeguro(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return "comprobante";

        var invalidos = Path.GetInvalidFileNameChars();
        var limpio = new string(nombre.Select(c => invalidos.Contains(c) ? ' ' : c).ToArray());
        limpio = string.Join(' ', limpio.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim(' ', '.');
        return limpio.Length == 0 ? "comprobante" : limpio;
    }
}
