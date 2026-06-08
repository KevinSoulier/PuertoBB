namespace Afip.Documentos;

public record EmisorDocumento
{
    public required string RazonSocial { get; init; }
    public required long Cuit { get; init; }
    public string? Domicilio { get; init; }
    public string? CondicionIva { get; init; }
    public string? IngresosBrutos { get; init; }
    public DateOnly? InicioActividades { get; init; }
    public byte[]? LogoPng { get; init; }
    public string ColorAcentoHex { get; init; } = "#1565C0";
}
