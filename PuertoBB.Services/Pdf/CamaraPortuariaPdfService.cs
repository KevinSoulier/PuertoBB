using Afip.Documentos;
using Afip.Documentos.Pdf;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Services;
using AfipConfig = PuertoBB.Core.Models.Afip.AfipConfig;
using PuertoBB.Services.Common;

namespace PuertoBB.Services.Pdf;

public class CamaraPortuariaPdfService : ICamaraPortuariaPdfService
{
    private const string RazonSocialEmisor = "Cámara Portuaria de Bahía Blanca";
    private static readonly PdfTheme _theme = PdfTheme.CamaraPortuaria;

    private readonly IAfipDocumentosService _documentos;
    private readonly IAfipConfigProvider _configProvider;

    public CamaraPortuariaPdfService(IAfipDocumentosService documentos, IAfipConfigProvider configProvider)
    {
        _documentos = documentos;
        _configProvider = configProvider;
    }

    public async Task<byte[]> GenerarPdfReciboAsync(Recibo recibo, CancellationToken ct = default)
    {
        var config = await _configProvider.GetAsync(ct);
        // Datos del receptor desde el snapshot fiscal del recibo (inmutable), no por navegación.
        var (tipoDoc, nroDoc) = Formato.ParseReceptorDoc(recibo.ReceptorCuit);
        var receptorNombre = recibo.ReceptorRazonSocial is { Length: > 0 } rs
            ? rs
            : recibo.ReceptorNombre is { Length: > 0 } n ? n : $"#{recibo.ClienteId}";

        var periodoDesde = new DateOnly(recibo.PeriodoAnio, recibo.PeriodoMes, 1);
        var periodoHasta = periodoDesde.AddMonths(1).AddDays(-1);

        // El detalle sale SIEMPRE del snapshot de líneas persistido (no se recalcula).
        var items = recibo.Lineas
            .OrderBy(l => l.Orden)
            .Select(l => new ItemDocumento { Descripcion = l.Descripcion, Cantidad = l.Cantidad, PrecioUnitario = l.PrecioUnitario, Subtotal = l.Importe })
            .ToList();

        var doc = new ComprobanteDocumento
        {
            CodigoTipo    = recibo.CodigoAfip,
            PuntoVenta    = recibo.PuntoDeVenta,
            Numero        = recibo.NumeroComprobante,
            FechaEmision  = recibo.FechaEmision,
            Cae           = recibo.CAE,
            FechaVencimientoCae = recibo.FechaVencimientoCAE,
            ImporteTotal  = recibo.Importe,
            Items         = items.Count > 0 ? items : [],
            ConceptoGeneral = items.Count > 0 ? null : recibo.Detalle,
            PeriodoServicioDesde = periodoDesde,
            PeriodoServicioHasta = periodoHasta,
            FechaVencimientoPago = recibo.FechaVencimientoPago,
            Emisor = BuildEmisor(config),
            Receptor = new ReceptorDocumento
            {
                RazonSocial   = receptorNombre,
                TipoDocumento = tipoDoc,
                NroDocumento  = nroDoc,
                Domicilio     = recibo.ReceptorDomicilio,
                CondicionIva  = recibo.ReceptorCondicionIva
            }
        };

        return _documentos.GenerarPdf(doc);
    }

    public async Task<byte[]> GenerarPdfNotaDeCreditoAsync(NotaDeCredito nc, CancellationToken ct = default)
    {
        var config = await _configProvider.GetAsync(ct);
        // N-7: sin el recibo original la NC saldría sin receptor ni comprobante asociado (normativa).
        var recibo = nc.ReciboOriginal
            ?? throw new InvalidOperationException("GenerarPdfNotaDeCredito requiere ReciboOriginal cargado (Include).");
        var comprobanteOriginal = Formato.Comprobante(recibo.PuntoDeVenta, recibo.NumeroComprobante);
        var (tipoDoc, nroDoc) = Formato.ParseReceptorDoc(recibo?.ReceptorCuit);
        var receptorNombre = recibo?.ReceptorRazonSocial is { Length: > 0 } rs
            ? rs
            : recibo?.ReceptorNombre ?? string.Empty;

        // Anulación total: la NC replica el detalle (líneas) del recibo original.
        var items = (recibo?.Lineas ?? [])
            .OrderBy(l => l.Orden)
            .Select(l => new ItemDocumento { Descripcion = l.Descripcion, Cantidad = l.Cantidad, PrecioUnitario = l.PrecioUnitario, Subtotal = l.Importe })
            .ToList();

        var doc = new ComprobanteDocumento
        {
            CodigoTipo    = nc.CodigoAfip,
            NombreOverride = "NOTA DE CRÉDITO",
            PuntoVenta    = nc.PuntoDeVenta,
            Numero        = nc.NumeroComprobante,
            FechaEmision  = nc.FechaEmision,
            Cae           = nc.CAE,
            FechaVencimientoCae = nc.FechaVencimientoCAE,
            ImporteTotal  = recibo?.Importe ?? 0,
            Items         = items.Count > 0 ? items : [],
            Leyendas      = [$"Anula el recibo original Nro. {comprobanteOriginal}"],
            ComprobanteAsociado = recibo is not null
                ? new ComprobanteAsociado(recibo.CodigoAfip, recibo.PuntoDeVenta, recibo.NumeroComprobante)
                : null,
            Emisor = BuildEmisor(config),
            Receptor = new ReceptorDocumento
            {
                RazonSocial  = receptorNombre,
                TipoDocumento = tipoDoc,
                NroDocumento  = nroDoc
            }
        };

        return _documentos.GenerarPdf(doc);
    }

    // Identidad fiscal del emisor para el PDF. La razón social sale de la configuración;
    // RazonSocialEmisor queda solo como respaldo si la base aún no tiene una cargada.
    private static EmisorDocumento BuildEmisor(AfipConfig config) => new()
    {
        RazonSocial       = string.IsNullOrWhiteSpace(config.RazonSocial) ? RazonSocialEmisor : config.RazonSocial,
        Cuit              = Formato.ParseCuit(config.CuitEmisor),
        CondicionIva      = "IVA Exento",
        ColorAcentoHex    = _theme.AcentoHex,
        LogoPng           = config.LogoPng is { Length: > 0 } ? config.LogoPng : null,
        IngresosBrutos    = config.IngresosBrutos,
        InicioActividades = config.InicioActividades.HasValue ? DateOnly.FromDateTime(config.InicioActividades.Value) : null
    };
}
