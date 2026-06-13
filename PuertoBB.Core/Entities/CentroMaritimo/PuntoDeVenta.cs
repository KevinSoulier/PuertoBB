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

    /// <summary>Nombre del archivo del certificado (solo para mostrar en la UI). El contenido real
    /// va en <see cref="CertificadoContenido"/>.</summary>
    public string? CertificadoRuta { get; set; }

    /// <summary>Contenido del certificado (.p12 en modo P12, .crt/.pem en modo CRT+KEY) guardado en la
    /// base. Texto plano. Null si no hay certificado cargado.</summary>
    public byte[]? CertificadoContenido { get; set; }

    /// <summary>Contraseña del certificado en texto plano. Null en modo CRT+KEY.</summary>
    public string? CertificadoPassword { get; set; }

    /// <summary>Nombre del archivo de la clave privada PEM (.key), solo para mostrar. El contenido va
    /// en <see cref="CertificadoKeyContenido"/>. Presente solo en modo CRT+KEY.</summary>
    public string? CertificadoKeyRuta { get; set; }

    /// <summary>Contenido de la clave privada PEM (.key) guardado en la base. Texto plano. Presente
    /// solo en modo CRT+KEY.</summary>
    public byte[]? CertificadoKeyContenido { get; set; }

    /// <summary>Punto de venta activo: el que usa la app para emitir. Solo uno puede estar activo.</summary>
    public bool Activo { get; set; }
}
