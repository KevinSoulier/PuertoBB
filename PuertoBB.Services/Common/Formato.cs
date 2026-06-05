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

    public static string Cuit(string cuit)
        => cuit.Length == 11 ? $"{cuit[..2]}-{cuit[2..10]}-{cuit[10..]}" : cuit;
}
