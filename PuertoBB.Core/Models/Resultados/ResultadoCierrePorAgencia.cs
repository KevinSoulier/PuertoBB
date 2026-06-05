namespace PuertoBB.Core.Models.Resultados;

/// <summary>Resultado del cierre de período (consolidación de vouchers) para una agencia.</summary>
public record ResultadoCierrePorAgencia
{
    public required int    AgenciaId     { get; init; }
    public required string AgenciaNombre { get; init; }
    public required bool   Exito         { get; init; }

    public int   CantidadVouchers { get; init; }
    public decimal Importe        { get; init; }
    public long?  NumeroComprobante { get; init; }

    public string? ErrorEmision { get; init; }
    public string? ErrorMail    { get; init; }

    public static ResultadoCierrePorAgencia Ok(int id, string nombre, int cantVouchers, decimal importe, long numero, string? errorMail = null)
        => new() { AgenciaId = id, AgenciaNombre = nombre, Exito = true, CantidadVouchers = cantVouchers, Importe = importe, NumeroComprobante = numero, ErrorMail = errorMail };

    public static ResultadoCierrePorAgencia Fallo(int id, string nombre, string error)
        => new() { AgenciaId = id, AgenciaNombre = nombre, Exito = false, ErrorEmision = error };

    public static ResultadoCierrePorAgencia Omitida(int id, string nombre, string motivo)
        => new() { AgenciaId = id, AgenciaNombre = nombre, Exito = false, ErrorEmision = motivo };
}
