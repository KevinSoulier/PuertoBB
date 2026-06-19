using Afip.Documentos;
using Afip.Documentos.Pdf;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;
using AfipConfig = PuertoBB.Core.Models.Afip.AfipConfig;
using PuertoBB.Services.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PuertoBB.Services.Pdf;

public class CentroMaritimoPdfService : ICentroMaritimoPdfService
{
    private const string RazonSocialEmisor = "Centro Marítimo de Bahía Blanca";

    private readonly IPdfMerger _merger;
    private readonly IAfipDocumentosService _documentos;
    private readonly IAfipConfigProvider _configProvider;

    public CentroMaritimoPdfService(IPdfMerger merger, IAfipDocumentosService documentos, IAfipConfigProvider configProvider)
    {
        _merger = merger;
        _documentos = documentos;
        _configProvider = configProvider;
    }

    // ── Recibo fiscal (delega en Afip.Documentos) ────────────────────────────

    public async Task<byte[]> GenerarPdfReciboAsync(Recibo recibo, CancellationToken ct = default)
        => _documentos.GenerarPdf(await BuildComprobanteAsync(recibo, [], ct));

    public async Task<byte[]> GenerarPdfConsolidadoAsync(Recibo recibo, IEnumerable<Voucher> vouchers, CancellationToken ct = default)
    {
        var lista = vouchers.OrderBy(v => v.Numero).ToList();
        return _documentos.GenerarPdf(await BuildComprobanteAsync(recibo, lista, ct));
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
            Emisor  = BuildEmisor(config),
            Receptor = new ReceptorDocumento
            {
                RazonSocial  = receptorNombre,
                TipoDocumento = tipoDoc,
                NroDocumento  = nroDoc
            }
        };

        return _documentos.GenerarPdf(doc);
    }

    // ── Voucher (no fiscal — template local, sin CAE/QR) ─────────────────────

    public async Task<byte[]> GenerarPdfVoucherAsync(Voucher voucher, CancellationToken ct = default)
    {
        var logo = (await _configProvider.GetAsync(ct)).LogoPng;
        return Document.Create(c => c.Page(p =>
        {
            ConfigurarPaginaVoucher(p);
            p.Content().Element(e => CuerpoVoucher(e, voucher, logo));
        })).GeneratePdf();
    }

    // ── Descarga / merge ─────────────────────────────────────────────────────

    public async Task<byte[]> GenerarPdfDescargaAsync(
        IReadOnlyList<Voucher> vouchers,
        Recibo? recibo,
        CancellationToken ct = default)
    {
        var partes = new List<byte[]>();

        if (recibo is not null)
            partes.Add(await GenerarPdfConsolidadoAsync(recibo, vouchers, ct));

        foreach (var v in vouchers.OrderBy(v => v.Numero))
        {
            ct.ThrowIfCancellationRequested();
            partes.Add(await GenerarPdfVoucherAsync(v, ct));
        }

        return _merger.Merge(partes);
    }

    // ── Helpers de construcción del ComprobanteDocumento ─────────────────────

    private async Task<ComprobanteDocumento> BuildComprobanteAsync(
        Recibo recibo,
        IReadOnlyList<Voucher> vouchers,
        CancellationToken ct)
    {
        var config = await _configProvider.GetAsync(ct);
        // Datos del receptor desde el snapshot fiscal del recibo (inmutable), no por navegación.
        var (tipoDoc, nroDoc) = Formato.ParseReceptorDoc(recibo.ReceptorCuit);
        var receptorNombre = recibo.ReceptorRazonSocial is { Length: > 0 } rs
            ? rs
            : recibo.ReceptorNombre is { Length: > 0 } n ? n : $"#{recibo.AgenciaId}";

        // El detalle sale SIEMPRE del snapshot de líneas persistido en el recibo (no se deriva de los
        // vouchers al mostrar), para que el comprobante se vea idéntico desde cualquier pantalla/PDF/mail.
        var items = recibo.Lineas
            .OrderBy(l => l.Orden)
            .Select(l => new ItemDocumento
            {
                Descripcion    = l.Descripcion,
                Cantidad       = l.Cantidad,
                PrecioUnitario = l.PrecioUnitario,
                Subtotal       = l.Importe
            }).ToList();

        var leyendas = new List<string>();

        var periodoDesde = new DateOnly(recibo.PeriodoAnio, recibo.PeriodoMes, 1);
        var periodoHasta = periodoDesde.AddMonths(1).AddDays(-1);

        return new ComprobanteDocumento
        {
            CodigoTipo    = recibo.CodigoAfip,
            PuntoVenta    = recibo.PuntoDeVenta,
            Numero        = recibo.NumeroComprobante,
            FechaEmision  = recibo.FechaEmision,
            Cae           = recibo.CAE,
            FechaVencimientoCae = recibo.FechaVencimientoCAE,
            ImporteTotal  = recibo.Importe,
            ConceptoGeneral = items.Count > 0 ? null : recibo.Detalle,
            Items         = items.Count > 0 ? items : [],
            Leyendas      = leyendas,
            PeriodoServicioDesde = periodoDesde,
            PeriodoServicioHasta = periodoHasta,
            FechaVencimientoPago = recibo.FechaVencimientoPago,
            Emisor  = BuildEmisor(config),
            Receptor = new ReceptorDocumento
            {
                RazonSocial   = receptorNombre,
                TipoDocumento = tipoDoc,
                NroDocumento  = nroDoc,
                Domicilio     = recibo.ReceptorDomicilio,
                CondicionIva  = recibo.ReceptorCondicionIva
            }
        };
    }

    private EmisorDocumento BuildEmisor(AfipConfig config)
    {
        return new EmisorDocumento
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

    // ── Template local del voucher (no fiscal) ────────────────────────────────

    private static readonly PdfTheme _theme = PdfTheme.CentroMaritimo;

    private static void ConfigurarPaginaVoucher(PageDescriptor p)
    {
        p.Size(PageSizes.A4);
        p.Margin(40);
        p.DefaultTextStyle(s => s.FontFamily(_theme.Fuente).FontSize(10).FontColor(_theme.Texto));
    }

    private static void CuerpoVoucher(IContainer e, Voucher voucher, byte[]? logo)
    {
        e.Column(col =>
        {
            col.Item().Element(x => EncabezadoVoucher(x, voucher, logo));

            col.Item().PaddingTop(20).Column(c =>
            {
                c.Spacing(10);
                c.Item().Row(r =>
                {
                    r.ConstantItem(150).Element(x => EtiquetaBilingue(x, "Buque", "Ship"));
                    r.RelativeItem().AlignMiddle().Text(voucher.Barco?.Nombre ?? $"#{voucher.BarcoId}");
                });
                c.Item().Row(r =>
                {
                    r.ConstantItem(150).Element(x => EtiquetaBilingue(x, "Fecha", "Date"));
                    r.RelativeItem().AlignMiddle().Text(Formato.Fecha(voucher.Fecha));
                });
            });

            col.Item().PaddingTop(16).Column(c =>
            {
                c.Item().Text("Contribución al Centro Marítimo de Bahía Blanca").FontColor(_theme.Texto);
                c.Item().Text("Contribution to the Maritime Center of Bahía Blanca")
                   .FontSize(9).Italic().FontColor(_theme.TextoSecundario);
            });

            col.Item().PaddingTop(14).Background("#F5F5F5").Padding(14).Row(r =>
            {
                r.RelativeItem().AlignMiddle().Element(x => EtiquetaBilingue(x, "Recibimos", "Received"));
                r.ConstantItem(170).AlignRight().AlignMiddle()
                 .Text(Formato.Moneda(voucher.Importe))
                 .FontSize(16).Bold().FontColor(_theme.Acento);
            });

            col.Item().PaddingTop(20).Column(c =>
            {
                c.Item().AlignRight().Text("Copia Agencia").FontSize(12).SemiBold();
                c.Item().AlignRight().Text("Copy Agency").FontSize(8).Italic().FontColor(_theme.TextoSecundario);
            });
        });
    }

    private static void EncabezadoVoucher(IContainer e, Voucher voucher, byte[]? logo)
    {
        e.BorderBottom(2).BorderColor(_theme.Acento).PaddingBottom(8).Row(row =>
        {
            if (logo is { Length: > 0 })
                row.ConstantItem(58).AlignMiddle().Image(logo).FitWidth();
            row.RelativeItem().PaddingLeft(12).AlignMiddle().Column(col =>
            {
                col.Item().Text("CENTRO MARÍTIMO").FontSize(15).Bold().FontColor(_theme.Acento);
                col.Item().Text("BAHÍA BLANCA").FontSize(11).SemiBold().FontColor(_theme.Acento);
            });
            row.ConstantItem(140).AlignMiddle().Column(col =>
            {
                col.Item().AlignRight().Text("Voucher / Comprobante").FontSize(8).FontColor(_theme.TextoSecundario);
                col.Item().AlignRight().Text($"N° {voucher.Numero:0000000}").FontSize(13).Bold();
            });
        });
    }

    private static void EtiquetaBilingue(IContainer e, string es, string en)
    {
        e.Column(col =>
        {
            col.Item().Text(es).SemiBold();
            col.Item().Text(en).FontSize(8).Italic().FontColor(_theme.TextoSecundario);
        });
    }
}
