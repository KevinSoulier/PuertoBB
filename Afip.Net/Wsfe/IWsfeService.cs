namespace Afip.Wsfe;

/// <summary>
/// Fachada de alto nivel del WebService de Facturación Electrónica (WSFE v1).
/// Resuelve internamente la autenticación (TA por servicio "wsfe") y el número de comprobante.
/// </summary>
public interface IWsfeService
{
    /// <summary>Healthcheck (FEDummy). No requiere certificado.</summary>
    Task<bool> VerificarServicioAsync(AfipOptions options, CancellationToken ct = default);

    /// <summary>Último número autorizado para (punto de venta, tipo). Requiere certificado (fuerza login WSAA).</summary>
    Task<long> UltimoComprobanteAsync(AfipOptions options, int puntoVenta, int codigoComprobante, CancellationToken ct = default);

    /// <summary>Solicita un CAE; obtiene internamente el número siguiente.</summary>
    Task<AfipCaeResult> SolicitarCaeAsync(AfipOptions options, AfipComprobanteRequest request, CancellationToken ct = default);
}
