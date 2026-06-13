namespace Afip.Abstractions;

/// <summary>
/// Cliente del WebService de Consulta a Padrón — Constancia de Inscripción
/// (<c>ws_sr_constancia_inscripcion</c>, ex ws_sr_padron_a5). Solo el método usado por PuertoBB.
/// </summary>
public interface IPadronClient
{
    /// <summary>getPersona_v2 — datos de la constancia de inscripción del CUIT consultado.
    /// Null si la persona no existe en el padrón de ARCA.</summary>
    Task<PadronPersona?> ConsultarAsync(string token, string sign, string cuitRepresentada, long idPersona,
        bool usarHomologacion, CancellationToken ct = default);
}

/// <summary>Constancia de inscripción resumida, neutra respecto del contrato SOAP.</summary>
public record PadronPersona
{
    /// <summary>Razón social (jurídica) o "APELLIDO NOMBRE" (física).</summary>
    public string? RazonSocial { get; init; }

    /// <summary>Domicilio fiscal formateado ("dirección, localidad, provincia (CP)").</summary>
    public string? Domicilio { get; init; }

    public bool EsPersonaJuridica { get; init; }

    /// <summary>Condición IVA del receptor sugerida (código RG 5616) derivada de los impuestos de la
    /// constancia: monotributo→6, IVA (30)→1, IVA exento (32)→4, ninguno→15.
    /// Null si la constancia vino con errores (ver <see cref="Observaciones"/>).</summary>
    public int? CondicionIvaSugeridaId { get; init; }

    /// <summary>Errores de la constancia informados por ARCA (persona existe pero sin constancia válida).</summary>
    public IReadOnlyList<string> Observaciones { get; init; } = [];
}
