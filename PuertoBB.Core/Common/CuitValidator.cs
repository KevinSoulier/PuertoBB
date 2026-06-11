namespace PuertoBB.Core.Common;

public static class CuitValidator
{
    private static readonly int[] _pesos = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

    public static bool EsValido(string? cuit)
    {
        if (cuit is null) return false;
        var digits = cuit.Where(char.IsDigit).Select(c => c - '0').ToArray();
        if (digits.Length != 11) return false;

        var suma = 0;
        for (var i = 0; i < 10; i++) suma += digits[i] * _pesos[i];
        var verificador = 11 - (suma % 11);
        if (verificador == 11) verificador = 0;
        if (verificador == 10) return false;
        return digits[10] == verificador;
    }
}
