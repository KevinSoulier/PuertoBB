using System.Collections.Concurrent;
using Afip.Abstractions;

namespace Afip.Mock;

/// <summary>
/// Cliente WSFE falso: simula FEDummy, FECompUltimoAutorizado y FECAESolicitar sin red.
/// Mantiene contadores por (puntoDeVenta, tipoComprobante) para emular numeración secuencial real.
/// </summary>
public sealed class MockWsfeClient : IWsfeClient
{
    private readonly ConcurrentDictionary<(int pv, int tipo), long> _contadores = new();

    public Task<bool> DummyAsync(bool usarHomologacion, CancellationToken ct = default)
        => Task.FromResult(true);

    public Task<long> UltimoComprobanteAsync(
        string token, string sign, string cuit,
        int puntoVenta, int tipoComprobante,
        bool usarHomologacion, CancellationToken ct = default)
    {
        var ultimo = _contadores.GetValueOrDefault((puntoVenta, tipoComprobante), 0L);
        return Task.FromResult(ultimo);
    }

    /// <summary>Respuesta fija de ConsultarComprobante (null = "no existe", como el default real).</summary>
    public WsfeComprobanteConsultado? ComprobanteConsultado { get; set; }

    public Task<WsfeComprobanteConsultado?> ConsultarComprobanteAsync(
        string token, string sign, string cuit,
        int puntoVenta, int tipoComprobante, long numero,
        bool usarHomologacion, CancellationToken ct = default)
        => Task.FromResult(ComprobanteConsultado);

    public Task<WsfeCaeResponse> SolicitarCaeAsync(
        string token, string sign, string cuit,
        WsfeCaeRequest request,
        bool usarHomologacion, CancellationToken ct = default)
    {
        // Actualiza el contador para que la próxima llamada a UltimoComprobante devuelva este número
        _contadores[(request.PuntoDeVenta, request.TipoComprobante)] = request.Numero;

        var cae = $"MOCK{DateTime.Now:yyyyMMddHHmmssff}";
        var respuesta = new WsfeCaeResponse
        {
            Aprobado = true,
            Cae = cae,
            FechaVencimientoCae = DateTime.Today.AddDays(10),
            Numero = request.Numero,
        };
        return Task.FromResult(respuesta);
    }

    /// <summary>Tablas FEParamGet* simuladas (setters públicos para tests).</summary>
    public IReadOnlyList<WsfePuntoVenta> PuntosVenta { get; set; } =
    [
        new() { Numero = 1, EmisionTipo = "CAE", Bloqueado = false },
        new() { Numero = 2, EmisionTipo = "CAE", Bloqueado = false },
    ];

    public IReadOnlyList<WsfeTipoComprobante> TiposComprobante { get; set; } =
    [
        new() { Id = 15, Descripcion = "Recibo C",          VigenteDesde = new DateTime(2010, 1, 1) },
        new() { Id = 13, Descripcion = "Nota de Crédito C", VigenteDesde = new DateTime(2010, 1, 1) },
        new() { Id = 11, Descripcion = "Factura C",         VigenteDesde = new DateTime(2010, 1, 1) },
        new() { Id = 6,  Descripcion = "Factura B",         VigenteDesde = new DateTime(2010, 1, 1) },
        new() { Id = 1,  Descripcion = "Factura A",         VigenteDesde = new DateTime(2010, 1, 1) },
    ];

    public IReadOnlyList<WsfeCondicionIvaReceptor> CondicionesIvaReceptor { get; set; } =
    [
        new() { Id = 1,  Descripcion = "IVA Responsable Inscripto" },
        new() { Id = 4,  Descripcion = "IVA Sujeto Exento" },
        new() { Id = 5,  Descripcion = "Consumidor Final" },
        new() { Id = 6,  Descripcion = "Responsable Monotributo" },
        new() { Id = 7,  Descripcion = "Sujeto No Categorizado" },
        new() { Id = 8,  Descripcion = "Proveedor del Exterior" },
        new() { Id = 9,  Descripcion = "Cliente del Exterior" },
        new() { Id = 10, Descripcion = "IVA Liberado – Ley N° 19.640" },
        new() { Id = 13, Descripcion = "Monotributista Social" },
        new() { Id = 15, Descripcion = "IVA No Alcanzado" },
        new() { Id = 16, Descripcion = "Monotributo Trabajador Independiente Promovido" },
    ];

    public Task<IReadOnlyList<WsfePuntoVenta>> ObtenerPuntosVentaAsync(string token, string sign, string cuit,
        bool usarHomologacion, CancellationToken ct = default)
        => Task.FromResult(PuntosVenta);

    public Task<IReadOnlyList<WsfeTipoComprobante>> ObtenerTiposComprobanteAsync(string token, string sign, string cuit,
        bool usarHomologacion, CancellationToken ct = default)
        => Task.FromResult(TiposComprobante);

    public Task<IReadOnlyList<WsfeCondicionIvaReceptor>> ObtenerCondicionesIvaReceptorAsync(string token, string sign, string cuit,
        string? claseComprobante, bool usarHomologacion, CancellationToken ct = default)
        => Task.FromResult(CondicionesIvaReceptor);
}
