namespace Afip.Documentos;

public record ReceptorDocumento
{
    public required string RazonSocial { get; init; }
    public required int TipoDocumento { get; init; }   // 80=CUIT, 96=DNI, 99=Consumidor Final
    public required long NroDocumento { get; init; }   // 0 si Consumidor Final
    public string? Domicilio { get; init; }
    public string? CondicionIva { get; init; }
}
