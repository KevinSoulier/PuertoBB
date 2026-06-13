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
            CondicionIvaReceptorId = request.CondicionIvaReceptorId,
            FechaComprobante = request.FechaComprobante,
            ImporteTotal = request.ImporteTotal,
            ServicioDesde = request.ServicioDesde,
            ServicioHasta = request.ServicioHasta,
            VencimientoPago = request.VencimientoPago,
            ComprobanteAsociado = request.ComprobanteAsociado is { } a
                ? new ComprobanteAsociadoWsfe { Tipo = a.Tipo, PuntoDeVenta = a.PuntoDeVenta, Numero = a.Numero, Cuit = a.Cuit }
                : null
        };

        WsfeCaeResponse resp;
        try
        {
            resp = await _wsfe.SolicitarCaeAsync(t.Token, t.Sign, options.Cuit, soap, options.UsarHomologacion, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // P2-2: la respuesta pudo perderse DESPUÉS de que AFIP autorizara `numero`.
            // Consultar el comprobante y, si coincide con lo pedido, adoptarlo en vez de fallar
            // (evita re-emitir con otro número un comprobante que ya existe fiscalmente).
            var reconciliado = await TryReconciliarAsync(t, options, request, numero, ct);
            if (reconciliado is not null) return reconciliado;
            // No se pudo confirmar: el comprobante PODRÍA existir en AFIP → señalizar como respuesta
            // perdida para que el caller advierta y NO reintente a ciegas.
            throw new AfipRespuestaPerdidaException(
                "Se pidió el CAE pero la respuesta de AFIP se perdió y no se pudo confirmar el comprobante.", ex);
        }

        return new AfipCaeResult
        {
            Aprobado = resp.Aprobado,
            Cae = resp.Cae,
            FechaVencimientoCae = resp.FechaVencimientoCae,
            Numero = resp.Numero == 0 ? numero : resp.Numero,
            Observaciones = resp.Observaciones
        };
    }

    public async Task<WsfeComprobanteConsultado?> ConsultarComprobanteAsync(AfipOptions options, int puntoVenta, int codigoComprobante, long numero, CancellationToken ct = default)
    {
        var t = await _ticket.GetTicketAsync(Servicio, options, ct);
        return await _wsfe.ConsultarComprobanteAsync(t.Token, t.Sign, options.Cuit, puntoVenta, codigoComprobante, numero, options.UsarHomologacion, ct);
    }

    public async Task<AfipCaeResult?> RecuperarSiYaEmitidoAsync(AfipOptions options, AfipComprobanteRequest request, CancellationToken ct = default)
    {
        var t = await _ticket.GetTicketAsync(Servicio, options, ct);

        // El último autorizado es el candidato natural: tras una respuesta perdida, AFIP lo dejó como
        // último para (PV, tipo). Si no avanzó respecto de lo nuestro, no hay nada que recuperar.
        var ultimo = await _wsfe.UltimoComprobanteAsync(
            t.Token, t.Sign, options.Cuit, request.PuntoDeVenta, request.CodigoComprobante, options.UsarHomologacion, ct);
        if (ultimo <= 0) return null;

        var c = await _wsfe.ConsultarComprobanteAsync(
            t.Token, t.Sign, options.Cuit, request.PuntoDeVenta, request.CodigoComprobante, ultimo, options.UsarHomologacion, ct);
        if (c is null || string.IsNullOrEmpty(c.Cae) || !Coincide(c, request)) return null;

        return new AfipCaeResult
        {
            Aprobado = true,
            Cae = c.Cae,
            FechaVencimientoCae = c.FechaVencimientoCae,
            Numero = c.Numero,
            Observaciones = "Recuperado vía FECompConsultar (respuesta previa perdida)."
        };
    }

    public async Task<IReadOnlyList<WsfePuntoVenta>> ObtenerPuntosVentaAsync(AfipOptions options, CancellationToken ct = default)
    {
        var t = await _ticket.GetTicketAsync(Servicio, options, ct);
        return await _wsfe.ObtenerPuntosVentaAsync(t.Token, t.Sign, options.Cuit, options.UsarHomologacion, ct);
    }

    public async Task<IReadOnlyList<WsfeTipoComprobante>> ObtenerTiposComprobanteAsync(AfipOptions options, CancellationToken ct = default)
    {
        var t = await _ticket.GetTicketAsync(Servicio, options, ct);
        return await _wsfe.ObtenerTiposComprobanteAsync(t.Token, t.Sign, options.Cuit, options.UsarHomologacion, ct);
    }

    public async Task<IReadOnlyList<WsfeCondicionIvaReceptor>> ObtenerCondicionesIvaReceptorAsync(AfipOptions options, string? claseComprobante = null, CancellationToken ct = default)
    {
        var t = await _ticket.GetTicketAsync(Servicio, options, ct);
        return await _wsfe.ObtenerCondicionesIvaReceptorAsync(t.Token, t.Sign, options.Cuit, claseComprobante, options.UsarHomologacion, ct);
    }

    private async Task<AfipCaeResult?> TryReconciliarAsync(AfipTicket t, AfipOptions options,
        AfipComprobanteRequest request, long numero, CancellationToken ct)
    {
        try
        {
            // La misma red intermitente que perdió la respuesta suele recuperarse en segundos:
            // reintentar la consulta con backoff corto antes de rendirse.
            var c = await ConsultarConReintentoAsync(t, options, request.PuntoDeVenta, request.CodigoComprobante, numero, ct);
            if (c is null || string.IsNullOrEmpty(c.Cae) || !Coincide(c, request)) return null;
            return new AfipCaeResult
            {
                Aprobado = true,
                Cae = c.Cae,
                FechaVencimientoCae = c.FechaVencimientoCae,
                Numero = c.Numero,
                Observaciones = "Reconciliado vía FECompConsultar tras error de comunicación."
            };
        }
        catch { return null; }   // la reconciliación nunca debe enmascarar el error original
    }

    private async Task<WsfeComprobanteConsultado?> ConsultarConReintentoAsync(AfipTicket t, AfipOptions options,
        int puntoVenta, int codigoComprobante, long numero, CancellationToken ct)
    {
        TimeSpan[] esperas = [TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1.5)];
        for (var intento = 0; ; intento++)
        {
            try
            {
                return await _wsfe.ConsultarComprobanteAsync(t.Token, t.Sign, options.Cuit,
                    puntoVenta, codigoComprobante, numero, options.UsarHomologacion, ct);
            }
            catch when (intento < esperas.Length && !ct.IsCancellationRequested)
            {
                await Task.Delay(esperas[intento], ct);
            }
        }
    }

    /// <summary>
    /// ¿El comprobante consultado en AFIP es el que se quiso emitir? Importes redondeados a 2 decimales
    /// (AFIP guarda el redondeado; el request puede traer más decimales) + DocNro + fecha del comprobante.
    /// </summary>
    private static bool Coincide(WsfeComprobanteConsultado c, AfipComprobanteRequest request)
        => Math.Round(c.ImporteTotal, 2, MidpointRounding.AwayFromZero)
               == Math.Round(request.ImporteTotal, 2, MidpointRounding.AwayFromZero)
        && c.DocNro == request.DocNroReceptor
        && c.FechaComprobante.Date == request.FechaComprobante.Date;
}
