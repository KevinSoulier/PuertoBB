namespace PuertoBB.Core.Models.Afip;

/// <summary>
/// Configuración necesaria para autenticar y facturar contra AFIP.
/// La provee cada app a partir de su entidad Configuracion (ver <see cref="Interfaces.Services.IAfipConfigProvider"/>).
/// </summary>
public record AfipConfig
{
    public required string   CuitEmisor              { get; init; } // sin guiones
    public string?           RazonSocial             { get; init; } // razón social del emisor (para el PDF)
    public byte[]?           CertificadoContenido    { get; init; } // bytes del .p12 o .crt/.pem
    public string?           CertificadoPassword     { get; init; }
    public byte[]?           CertificadoKeyContenido { get; init; } // bytes del .key (modo CRT+KEY)
    public required bool     UsarHomologacion        { get; init; }
    public string?           IngresosBrutos      { get; init; }
    public DateTime?         InicioActividades   { get; init; }
    public byte[]?           LogoPng             { get; init; } // logo del emisor para el header del PDF
}
