using PuertoBB.Core.Common;
using PuertoBB.Core.Models.Afip;

namespace PuertoBB.Core.Interfaces.Services;

/// <summary>
/// Servicio AFIP/ARCA compartido por ambas apps. Orquesta WSAA (ticket) + WSFE (CAE).
/// </summary>
public interface IAfipService
{
    /// <summary>Solicita un CAE para el comprobante. Obtiene internamente el número siguiente.</summary>
    Task<ServiceResult<CaeResult>> ObtenerCAEAsync(ComprobanteAfipRequest request, CancellationToken ct = default);

    /// <summary>
    /// Recuperación ante respuesta perdida (crash/cancelación de un intento previo): consulta el último
    /// comprobante de AFIP y, si coincide con <paramref name="request"/>, devuelve su CAE para adoptarlo
    /// en vez de re-emitir (evita duplicar). <c>Data == null</c> = no hay nada que recuperar (emitir normalmente).
    /// Best-effort: ante error de red devuelve <c>Ok(null)</c> (no bloquea la emisión).
    /// </summary>
    Task<ServiceResult<CaeResult?>> RecuperarComprobanteAsync(ComprobanteAfipRequest request, CancellationToken ct = default);

    /// <summary>Healthcheck del servicio WSFE (FEDummy).</summary>
    Task<ServiceResult<bool>> VerificarServicioAsync(CancellationToken ct = default);

    /// <summary>
    /// Diagnóstico completo de conexión: verifica el servicio (FEDummy) y la autenticación con el
    /// certificado consultando el último comprobante para (puntoVenta, codigoComprobante).
    /// </summary>
    Task<ServiceResult<DiagnosticoAfip>> ProbarConexionAsync(int puntoVenta, int codigoComprobante, CancellationToken ct = default);
}
