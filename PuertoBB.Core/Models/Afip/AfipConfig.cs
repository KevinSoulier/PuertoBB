namespace PuertoBB.Core.Models.Afip;

/// <summary>
/// Configuración necesaria para autenticar y facturar contra AFIP.
/// La provee cada app a partir de su entidad Configuracion (ver <see cref="Interfaces.Services.IAfipConfigProvider"/>).
/// </summary>
public record AfipConfig
{
    public required string  CuitEmisor       { get; init; } // sin guiones
    public string?          CertificadoRuta  { get; init; }
    public string?          CertificadoPassword { get; init; }
    public required bool    UsarHomologacion { get; init; }
}
