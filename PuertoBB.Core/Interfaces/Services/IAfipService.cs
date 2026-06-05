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

    /// <summary>Healthcheck del servicio WSFE (FEDummy).</summary>
    Task<ServiceResult<bool>> VerificarServicioAsync(CancellationToken ct = default);
}
