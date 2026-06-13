using Afip.Abstractions;

namespace Afip.Padron;

/// <summary>
/// Fachada de alto nivel de Consulta a Padrón — Constancia de Inscripción.
/// Resuelve internamente la autenticación (TA del servicio <c>ws_sr_constancia_inscripcion</c>,
/// que debe estar delegado al certificado en ARCA además de wsfe).
/// </summary>
public interface IPadronService
{
    /// <summary>Constancia de inscripción del CUIT consultado; null si no existe en el padrón.</summary>
    Task<PadronPersona?> ConsultarPersonaAsync(AfipOptions options, long cuit, CancellationToken ct = default);
}
