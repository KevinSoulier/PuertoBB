using PuertoBB.Core.Entities.Common;

namespace PuertoBB.Core.Entities.CentroMaritimo;

/// <summary>
/// Punto de venta AFIP con su ambiente y certificado. Se pueden cargar varios (ej. uno de
/// homologación y otro de producción) y uno queda marcado como <see cref="Activo"/>: el activo
/// determina el número de PV, el ambiente y el certificado usados al emitir.
/// </summary>
public class PuntoDeVenta : BaseEntity
{
    public int ConfiguracionId { get; set; }

    /// <summary>Etiqueta para identificarlo (ej. "Homologación", "Producción").</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Número de punto de venta habilitado en AFIP.</summary>
    public int Numero { get; set; }

    /// <summary>true = homologación (pruebas); false = producción.</summary>
    public bool UsarHomologacion { get; set; }

    /// <summary>Ruta al certificado (.p12 en modo P12, .crt/.pem en modo CRT+KEY).</summary>
    public string? CertificadoRuta { get; set; }

    /// <summary>Contraseña del certificado cifrada en reposo. Null en modo CRT+KEY.</summary>
    public string? CertificadoPassword { get; set; }

    /// <summary>Ruta a la clave privada PEM (.key). Presente solo en modo CRT+KEY.</summary>
    public string? CertificadoKeyRuta { get; set; }

    /// <summary>Punto de venta activo: el que usa la app para emitir. Solo uno puede estar activo.</summary>
    public bool Activo { get; set; }
}
