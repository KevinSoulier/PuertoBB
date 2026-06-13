namespace Afip;

/// <summary>
/// Parámetros para autenticar y operar contra AFIP/ARCA. Se pasan por llamada (no se cachean en DI),
/// de modo que la configuración puede cambiar en runtime sin reiniciar la aplicación.
/// </summary>
public sealed record AfipOptions
{
    /// <summary>CUIT del emisor / titular del certificado. Solo dígitos (sin guiones).</summary>
    public required string Cuit { get; init; }

    /// <summary>Ruta al certificado. En modo P12: archivo .p12/.pfx. En modo CRT+KEY: archivo .crt/.pem.
    /// Se ignora si se provee <see cref="CertificadoContenido"/>.</summary>
    public string? CertificadoRuta { get; init; }

    /// <summary>Contenido del certificado en memoria (.p12 o .crt/.pem). Si está presente tiene prioridad
    /// sobre <see cref="CertificadoRuta"/> y no se toca el disco.</summary>
    public byte[]? CertificadoContenido { get; init; }

    /// <summary>Contraseña del certificado. Solo aplica en modo P12. En modo CRT+KEY debe ser null.</summary>
    public string? CertificadoPassword { get; init; }

    /// <summary>Ruta a la clave privada PEM (.key). Cuando está presente se usa modo CRT+KEY en lugar de P12.
    /// Se ignora si se provee <see cref="CertificadoKeyContenido"/>.</summary>
    public string? CertificadoKeyRuta { get; init; }

    /// <summary>Contenido de la clave privada PEM (.key) en memoria. Si está presente (o lo está
    /// <see cref="CertificadoKeyRuta"/>) se usa modo CRT+KEY; tiene prioridad sobre la ruta.</summary>
    public byte[]? CertificadoKeyContenido { get; init; }

    /// <summary>true = ambiente de homologación (testing); false = producción.</summary>
    public required bool UsarHomologacion { get; init; }
}
