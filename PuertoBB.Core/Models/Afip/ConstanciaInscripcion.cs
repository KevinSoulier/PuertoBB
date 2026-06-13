namespace PuertoBB.Core.Models.Afip;

/// <summary>
/// Constancia de inscripción de un CUIT en el padrón de ARCA
/// (resultado de "Validar CUIT en ARCA" en el ABM de empresas/agencias).
/// </summary>
public record ConstanciaInscripcion
{
    /// <summary>Razón social (jurídica) o "APELLIDO NOMBRE" (física).</summary>
    public string? RazonSocial { get; init; }

    /// <summary>Domicilio fiscal formateado.</summary>
    public string? Domicilio { get; init; }

    /// <summary>Condición frente al IVA sugerida (código del catálogo RG 5616); null si la constancia
    /// vino con errores y no se puede derivar.</summary>
    public int? CondicionIvaId { get; init; }

    /// <summary>Observaciones de ARCA (p. ej. "no alcanzado por la constancia de inscripción").</summary>
    public IReadOnlyList<string> Observaciones { get; init; } = [];
}
