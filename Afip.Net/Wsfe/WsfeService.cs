using Afip.Abstractions;
using Afip.Wsaa;

namespace Afip.Wsfe;

/// <summary>
/// Implementación de <see cref="IWsfeService"/>: obtiene el TA (servicio "wsfe") vía
/// <see cref="ITicketProvider"/>, resuelve el número con FECompUltimoAutorizado y solicita el CAE.
/// </summary>
public sealed class WsfeService : IWsfeService
{
    private const string Servicio = "wsfe";

    private readonly ITicketProvider _ticket;
    private readonly IWsfeClient _wsfe;

    public WsfeService(ITicketProvider ticket, IWsfeClient wsfe)
    {
        _ticket = ticket;
        _wsfe = wsfe;
    }

    public Task<bool> VerificarServicioAsync(AfipOptions options, CancellationToken ct = default)
        => _wsfe.DummyAsync(options.UsarHomologacion, ct);

    public async Task<long> UltimoComprobanteAsync(AfipOptions options, int puntoVenta, int codigoComprobante, CancellationToken ct = default)
    {
        var t = await _ticket.GetTicketAsync(Servicio, options, ct);
        return await _wsfe.UltimoComprobanteAsync(t.Token, t.Sign, options.Cuit, puntoVenta, codigoComprobante, options.UsarHomologacion, ct);
    }

    public async Task<AfipCaeResult> SolicitarCaeAsync(AfipOptions options, AfipComprobanteRequest request, CancellationToken ct = default)
    {
        var t = await _ticket.GetTicketAsync(Servicio, options, ct);

        var numero = await _wsfe.UltimoComprobanteAsync(
            t.Token, t.Sign, options.Cuit, request.PuntoDeVenta, request.CodigoComprobante, options.UsarHomologacion, ct) + 1;

        var soap = new WsfeCaeRequest
        {
            TipoComprobante = request.CodigoComprobante,
            PuntoDeVenta = request.PuntoDeVenta,
            Numero = numero,
            Concepto = request.Concepto,
            DocTipo = request.DocTipoReceptor,
            DocNro = request.DocNroReceptor,
            FechaComprobante = request.FechaComprobante,
            ImporteTotal = request.ImporteTotal,
            ServicioDesde = request.ServicioDesde,
            ServicioHasta = request.ServicioHasta,
            VencimientoPago = request.VencimientoPago,
            ComprobanteAsociado = request.ComprobanteAsociado is { } a
                ? new ComprobanteAsociadoWsfe { Tipo = a.Tipo, PuntoDeVenta = a.PuntoDeVenta, Numero = a.Numero, Cuit = a.Cuit }
                : null
        };

        var resp = await _wsfe.SolicitarCaeAsync(t.Token, t.Sign, options.Cuit, soap, options.UsarHomologacion, ct);

        return new AfipCaeResult
        {
            Aprobado = resp.Aprobado,
            Cae = resp.Cae,
            FechaVencimientoCae = resp.FechaVencimientoCae,
            Numero = resp.Numero == 0 ? numero : resp.Numero,
            Observaciones = resp.Observaciones
        };
    }
}
