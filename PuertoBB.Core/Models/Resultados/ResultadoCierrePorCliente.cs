namespace PuertoBB.Core.Models.Resultados;

/// <summary>Resultado del cierre de período (consolidación de vouchers) para una agencia.</summary>
public record ResultadoCierrePorCliente
{
    public required int    ClienteId     { get; init; }
    public required string ClienteNombre { get; init; }
    public required bool   Exito         { get; init; }

    public int   CantidadVouchers { get; init; }
    public decimal Importe        { get; init; }
    public long?  NumeroComprobante { get; init; }

    public string? ErrorEmision { get; init; }
    public string? ErrorMail    { get; init; }

    public static ResultadoCierrePorCliente Ok(int id, string nombre, int cantVouchers, decimal importe, long numero, string? errorMail = null)
        => new() { ClienteId = id, ClienteNombre = nombre, Exito = true, CantidadVouchers = cantVouchers, Importe = importe, NumeroComprobante = numero, ErrorMail = errorMail };

    public static ResultadoCierrePorCliente Fallo(int id, string nombre, string error)
        => new() { ClienteId = id, ClienteNombre = nombre, Exito = false, ErrorEmision = error };

    public static ResultadoCierrePorCliente Omitida(int id, string nombre, string motivo)
        => new() { ClienteId = id, ClienteNombre = nombre, Exito = false, ErrorEmision = motivo };
}
