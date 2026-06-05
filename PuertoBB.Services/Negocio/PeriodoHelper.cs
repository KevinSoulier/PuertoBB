namespace PuertoBB.Services.Negocio;

/// <summary>Utilidades de período (anio/mes) y CUIT para armar requests AFIP.</summary>
public static class PeriodoHelper
{
    public static int PrimerDia(int anio, int mes) => int.Parse($"{anio:0000}{mes:00}01");

    public static int UltimoDia(int anio, int mes)
        => int.Parse($"{anio:0000}{mes:00}{DateTime.DaysInMonth(anio, mes):00}");

    public static string SoloDigitos(string? cuit)
        => new(( cuit ?? "").Where(char.IsDigit).ToArray());
}
