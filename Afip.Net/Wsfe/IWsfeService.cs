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

    /// <summary>Solicita un CAE; obtiene internamente el número siguiente. Si la respuesta se pierde
    /// (error de comunicación post-aprobación), reconcilia vía FECompConsultar antes de fallar.</summary>
    Task<AfipCaeResult> SolicitarCaeAsync(AfipOptions options, AfipComprobanteRequest request, CancellationToken ct = default);

    /// <summary>Consulta un comprobante ya autorizado (FECompConsultar); null si no existe.</summary>
    Task<Afip.Abstractions.WsfeComprobanteConsultado?> ConsultarComprobanteAsync(AfipOptions options, int puntoVenta, int codigoComprobante, long numero, CancellationToken ct = default);

    /// <summary>
    /// Recuperación ante respuesta perdida (crash/cancelación): consulta el último comprobante
    /// autorizado para (PV, tipo) y, si coincide con <paramref name="request"/> (importe/DocNro/fecha),
    /// devuelve su CAE en vez de re-emitir. null = no hay comprobante que coincida (emitir normalmente).
    /// </summary>
    Task<AfipCaeResult?> RecuperarSiYaEmitidoAsync(AfipOptions options, AfipComprobanteRequest request, CancellationToken ct = default);

    /// <summary>Puntos de venta habilitados para WS (FEParamGetPtosVenta). Vacío es habitual en homologación.</summary>
    Task<IReadOnlyList<Afip.Abstractions.WsfePuntoVenta>> ObtenerPuntosVentaAsync(AfipOptions options, CancellationToken ct = default);

    /// <summary>Tipos de comprobante vigentes (FEParamGetTiposCbte).</summary>
    Task<IReadOnlyList<Afip.Abstractions.WsfeTipoComprobante>> ObtenerTiposComprobanteAsync(AfipOptions options, CancellationToken ct = default);

    /// <summary>Condiciones IVA de receptor válidas (FEParamGetCondicionIvaReceptor, RG 5616). Clase "A"/"B"/"C" o null = todas.</summary>
    Task<IReadOnlyList<Afip.Abstractions.WsfeCondicionIvaReceptor>> ObtenerCondicionesIvaReceptorAsync(AfipOptions options, string? claseComprobante = null, CancellationToken ct = default);
}
