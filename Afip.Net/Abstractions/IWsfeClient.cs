namespace Afip.Abstractions;

/// <summary>
/// Cliente del WebService de Facturación Electrónica (WSFE v1). Solo los métodos usados por PuertoBB.
/// La implementación concreta se genera desde el WSDL con dotnet-svcutil (ver afip-integracion.md).
/// </summary>
public interface IWsfeClient
{
    /// <summary>FEDummy — healthcheck.</summary>
    Task<bool> DummyAsync(bool usarHomologacion, CancellationToken ct = default);

    /// <summary>FECompUltimoAutorizado — último número emitido para (tipo, punto de venta).</summary>
    Task<long> UltimoComprobanteAsync(string token, string sign, string cuit, int puntoVenta, int tipoComprobante, bool usarHomologacion, CancellationToken ct = default);

    /// <summary>FECAESolicitar — solicita el CAE para un comprobante.</summary>
    Task<WsfeCaeResponse> SolicitarCaeAsync(string token, string sign, string cuit, WsfeCaeRequest request, bool usarHomologacion, CancellationToken ct = default);

    /// <summary>FECompConsultar — datos de un comprobante ya autorizado; null si no existe (error 602).</summary>
    Task<WsfeComprobanteConsultado?> ConsultarComprobanteAsync(string token, string sign, string cuit,
        int puntoVenta, int tipoComprobante, long numero, bool usarHomologacion, CancellationToken ct = default);

    /// <summary>FEParamGetPtosVenta — puntos de venta habilitados para Web Services del CUIT.</summary>
    Task<IReadOnlyList<WsfePuntoVenta>> ObtenerPuntosVentaAsync(string token, string sign, string cuit,
        bool usarHomologacion, CancellationToken ct = default);

    /// <summary>FEParamGetTiposCbte — tipos de comprobante vigentes según AFIP.</summary>
    Task<IReadOnlyList<WsfeTipoComprobante>> ObtenerTiposComprobanteAsync(string token, string sign, string cuit,
        bool usarHomologacion, CancellationToken ct = default);

    /// <summary>FEParamGetCondicionIvaReceptor — condiciones IVA de receptor válidas (RG 5616).
    /// <paramref name="claseComprobante"/> = "A"/"B"/"C" para filtrar; null = todas.</summary>
    Task<IReadOnlyList<WsfeCondicionIvaReceptor>> ObtenerCondicionesIvaReceptorAsync(string token, string sign, string cuit,
        string? claseComprobante, bool usarHomologacion, CancellationToken ct = default);
}

/// <summary>Punto de venta informado por FEParamGetPtosVenta.</summary>
public record WsfePuntoVenta
{
    public required int    Numero      { get; init; }
    public string?         EmisionTipo { get; init; }   // "CAE" | "CAEA"
    public bool            Bloqueado   { get; init; }
    public DateTime?       FechaBaja   { get; init; }
}

/// <summary>Tipo de comprobante informado por FEParamGetTiposCbte.</summary>
public record WsfeTipoComprobante
{
    public required int    Id          { get; init; }
    public string?         Descripcion { get; init; }
    public DateTime?       VigenteDesde { get; init; }
    public DateTime?       VigenteHasta { get; init; }
}

/// <summary>Condición IVA del receptor informada por FEParamGetCondicionIvaReceptor.</summary>
public record WsfeCondicionIvaReceptor
{
    public required int    Id          { get; init; }
    public string?         Descripcion { get; init; }
    public string?         ClaseComprobante { get; init; }
}

/// <summary>Comprobante ya autorizado, devuelto por FECompConsultar.</summary>
public record WsfeComprobanteConsultado
{
    public required long     Numero              { get; init; }
    public required decimal  ImporteTotal        { get; init; }
    public required long     DocNro              { get; init; }
    public required DateTime FechaComprobante    { get; init; }
    public string?           Cae                 { get; init; }
    public DateTime?         FechaVencimientoCae { get; init; }
}

/// <summary>Datos de un comprobante para FECAESolicitar. Tipo C: el total va en ImpNeto; IVA y OpEx = 0, sin array Iva.</summary>
public record WsfeCaeRequest
{
    public required int      TipoComprobante { get; init; }
    public required int      PuntoDeVenta    { get; init; }
    public required long     Numero          { get; init; }
    public required int      Concepto        { get; init; } // 2 = Servicios
    public required int      DocTipo         { get; init; } // 80 = CUIT
    public required long     DocNro          { get; init; }
    public required int      CondicionIvaReceptorId { get; init; } // RG 5616, obligatorio
    public required DateTime FechaComprobante { get; init; }
    public required decimal  ImporteTotal    { get; init; }
    public required int      ServicioDesde   { get; init; } // yyyyMMdd
    public required int      ServicioHasta   { get; init; }
    public required DateTime VencimientoPago { get; init; }
    public ComprobanteAsociadoWsfe? ComprobanteAsociado { get; init; }
}

public record ComprobanteAsociadoWsfe
{
    public required int  Tipo         { get; init; }
    public required int  PuntoDeVenta { get; init; }
    public required long Numero       { get; init; }
    public required long Cuit         { get; init; }
}

/// <summary>Respuesta de FECAESolicitar.</summary>
public record WsfeCaeResponse
{
    public required bool     Aprobado            { get; init; } // Resultado == "A"
    public string?           Cae                 { get; init; }
    public DateTime?         FechaVencimientoCae { get; init; }
    public long              Numero              { get; init; }
    public string?           Observaciones       { get; init; }
}
