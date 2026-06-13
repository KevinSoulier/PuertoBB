using Afip.Abstractions;

namespace Afip.Mock;

/// <summary>
/// Cliente de padrón falso: respuestas deterministas sin red ni certificado.
/// CUITs centinela para simular los tres escenarios del servicio real.
/// </summary>
public sealed class MockPadronClient : IPadronClient
{
    /// <summary>CUIT que simula "persona inexistente en el padrón" (devuelve null).</summary>
    public const long CuitInexistente = 20000000001;

    /// <summary>CUIT que simula constancia con errores (datos parciales, sin condición sugerida).</summary>
    public const long CuitConErrorConstancia = 20000000002;

    public Task<PadronPersona?> ConsultarAsync(string token, string sign, string cuitRepresentada, long idPersona,
        bool usarHomologacion, CancellationToken ct = default)
        => Task.FromResult<PadronPersona?>(idPersona switch
        {
            CuitInexistente => null,
            CuitConErrorConstancia => new PadronPersona
            {
                RazonSocial = "PERSONA SIN CONSTANCIA",
                CondicionIvaSugeridaId = null,
                Observaciones = ["El contribuyente no se encuentra alcanzado por la constancia de inscripción (simulado)."]
            },
            _ => new PadronPersona
            {
                RazonSocial = "EMPRESA DEMO S.A.",
                Domicilio = "Av. Siempreviva 742, Bahía Blanca, Buenos Aires (CP 8000)",
                EsPersonaJuridica = true,
                CondicionIvaSugeridaId = 1
            }
        });
}
