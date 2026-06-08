namespace Afip;

/// <summary>
/// Parámetros para autenticar y operar contra AFIP/ARCA. Se pasan por llamada (no se cachean en DI),
/// de modo que la configuración puede cambiar en runtime sin reiniciar la aplicación.
/// </summary>
public sealed record AfipOptions
{
    /// <summary>CUIT del emisor / titular del certificado. Solo dígitos (sin guiones).</summary>
    public required string Cuit { get; init; }

    /// <summary>Ruta al certificado. En modo P12: archivo .p12/.pfx. En modo CRT+KEY: archivo .crt/.pem.</summary>
    public string? CertificadoRuta { get; init; }

    /// <summary>Contraseña del certificado. Solo aplica en modo P12. En modo CRT+KEY debe ser null.</summary>
    public string? CertificadoPassword { get; init; }

    /// <summary>Ruta a la clave privada PEM (.key). Cuando está presente se usa modo CRT+KEY en lugar de P12.</summary>
    public string? CertificadoKeyRuta { get; init; }

    /// <summary>true = ambiente de homologación (testing); false = producción.</summary>
    public required bool UsarHomologacion { get; init; }
}
