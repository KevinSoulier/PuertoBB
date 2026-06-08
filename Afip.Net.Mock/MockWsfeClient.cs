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
}
