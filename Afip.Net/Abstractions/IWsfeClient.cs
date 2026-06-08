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
}

/// <summary>Datos de un comprobante para FECAESolicitar. ImpNeto/IVA = 0 para tipo C (exento).</summary>
public record WsfeCaeRequest
{
    public required int      TipoComprobante { get; init; }
    public required int      PuntoDeVenta    { get; init; }
    public required long     Numero          { get; init; }
    public required int      Concepto        { get; init; } // 2 = Servicios
    public required int      DocTipo         { get; init; } // 80 = CUIT
    public required long     DocNro          { get; init; }
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
