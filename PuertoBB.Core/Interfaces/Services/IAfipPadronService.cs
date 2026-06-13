using PuertoBB.Core.Common;
using PuertoBB.Core.Models.Afip;

namespace PuertoBB.Core.Interfaces.Services;

/// <summary>
/// Consulta de constancia de inscripción en el padrón de ARCA (servicio
/// <c>ws_sr_constancia_inscripcion</c>, que debe estar delegado al certificado además de wsfe).
/// </summary>
public interface IAfipPadronService
{
    /// <summary>
    /// Consulta el CUIT en el padrón. <c>Ok(datos)</c> si existe; <c>Ok(null)</c> si no figura
    /// en el padrón; <c>Fail</c> ante CUIT inválido, falta de certificado o error del servicio.
    /// </summary>
    Task<ServiceResult<ConstanciaInscripcion?>> ConsultarCuitAsync(string cuit, CancellationToken ct = default);
}
