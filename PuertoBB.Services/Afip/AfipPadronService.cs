using global::Afip;
using global::Afip.Padron;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Common;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Afip;

namespace PuertoBB.Services.Afip;

/// <summary>
/// Adaptador entre el dominio PuertoBB (<see cref="IAfipPadronService"/>) y el cliente de padrón
/// de Afip.Net (<see cref="IPadronService"/>). Valida el CUIT localmente antes de consultar y
/// traduce el fallo típico de "servicio no delegado" a un mensaje accionable.
/// </summary>
public class AfipPadronService : IAfipPadronService
{
    private readonly IPadronService _padron;
    private readonly IAfipConfigProvider _configProvider;
    private readonly ILogger<AfipPadronService> _logger;

    public AfipPadronService(IPadronService padron, IAfipConfigProvider configProvider, ILogger<AfipPadronService> logger)
    {
        _padron = padron;
        _configProvider = configProvider;
        _logger = logger;
    }

    public async Task<ServiceResult<ConstanciaInscripcion?>> ConsultarCuitAsync(string cuit, CancellationToken ct = default)
    {
        if (!CuitValidator.EsValido(cuit))
            return ServiceResult<ConstanciaInscripcion?>.Fail("El CUIT no es válido (revise los 11 dígitos).");

        try
        {
            var config = await _configProvider.GetAsync(ct);
            if (config.CertificadoContenido is not { Length: > 0 })
                return ServiceResult<ConstanciaInscripcion?>.Fail(
                    "No hay certificado AFIP configurado: cargue el certificado en Configuración para consultar el padrón.");

            var soloDigitos = long.Parse(new string(cuit.Where(char.IsDigit).ToArray()));
            var persona = await _padron.ConsultarPersonaAsync(ToOptions(config), soloDigitos, ct);

            if (persona is null)
                return ServiceResult<ConstanciaInscripcion?>.Ok(null);   // no figura en el padrón

            return ServiceResult<ConstanciaInscripcion?>.Ok(new ConstanciaInscripcion
            {
                RazonSocial = persona.RazonSocial,
                Domicilio = persona.Domicilio,
                CondicionIvaId = persona.CondicionIvaSugeridaId,
                Observaciones = persona.Observaciones
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Consulta de padrón falló para CUIT {Cuit}", cuit);
            // El fallo más común: el TA no se puede emitir porque el servicio no está delegado al certificado.
            var hint = ex.Message.Contains("cms", StringComparison.OrdinalIgnoreCase)
                    || ex.Message.Contains("autoriza", StringComparison.OrdinalIgnoreCase)
                    || ex.Message.Contains("denied", StringComparison.OrdinalIgnoreCase)
                ? " Verifique que el servicio 'Consulta a Padrón - Constancia de inscripción' (ws_sr_constancia_inscripcion) esté delegado al certificado en el Administrador de Relaciones de ARCA."
                : string.Empty;
            return ServiceResult<ConstanciaInscripcion?>.Fail($"No se pudo consultar el padrón: {ex.Message}.{hint}");
        }
    }

    private static AfipOptions ToOptions(AfipConfig c) => new()
    {
        Cuit = c.CuitEmisor,
        CertificadoContenido = c.CertificadoContenido,
        CertificadoPassword = c.CertificadoPassword,
        CertificadoKeyContenido = c.CertificadoKeyContenido,
        UsarHomologacion = c.UsarHomologacion
    };
}
