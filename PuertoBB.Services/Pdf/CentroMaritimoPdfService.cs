using Afip.Documentos;
using Afip.Documentos.Pdf;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;
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

    private static readonly byte[] LogoBytes = CargarLogo();

    public CentroMaritimoPdfService(IPdfMerger merger, IAfipDocumentosService documentos, IAfipConfigProvider configProvider)
    {
        _merger = merger;
        _documentos = documentos;
        _configProvider = configProvider;
    }

    private static byte[] CargarLogo()
    {
        try
        {
            var asm = typeof(CentroMaritimoPdfService).Assembly;
            using var s = asm.GetManifestResourceStream("PuertoBB.Services.Assets.CMBB_Logo.png");
            if (s is null) return [];
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }
        catch { return []; }
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
        var recibo = nc.ReciboOriginal;
        var (tipoDoc, nroDoc) = Formato.ParseReceptorDoc(recibo?.Agencia?.Cuit);
        var receptorNombre = recibo?.Agencia?.RazonSocial is { Length: > 0 } rs
            ? rs
            : recibo?.Agencia?.Nombre ?? string.Empty;

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
            Leyendas      = [$"Anula el recibo original Nro. {nc.ReciboOriginalId}"],
            ComprobanteAsociado = recibo is not null
                ? new ComprobanteAsociado(recibo.CodigoAfip, recibo.PuntoDeVenta, recibo.NumeroComprobante)
                : null,
            Emisor  = BuildEmisor(config.CuitEmisor, recibo),
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

    public Task<byte[]> GenerarPdfVoucherAsync(Voucher voucher, CancellationToken ct = default)
    {
        var bytes = Document.Create(c => c.Page(p =>
        {
            ConfigurarPaginaVoucher(p);
            p.Content().Element(e => CuerpoVoucher(e, voucher));
        })).GeneratePdf();
        return Task.FromResult(bytes);
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
        var (tipoDoc, nroDoc) = Formato.ParseReceptorDoc(recibo.Agencia?.Cuit);
        var receptorNombre = recibo.Agencia?.RazonSocial is { Length: > 0 } rs
            ? rs
            : recibo.Agencia?.Nombre ?? $"#{recibo.AgenciaId}";

        var items = vouchers.Select(v => new ItemDocumento
        {
            Descripcion   = $"Voucher {v.Numero} — {v.Barco?.Nombre ?? $"#{v.BarcoId}"} — {Formato.Fecha(v.Fecha)}",
            PrecioUnitario = v.Importe,
            Subtotal       = v.Importe
        }).ToList();

        var leyendas = new List<string>();
        if (recibo.EsApoderado && !string.IsNullOrWhiteSpace(recibo.NombreApoderado))
            leyendas.Add($"Emite (apoderado): {recibo.NombreApoderado} — CUIT {Formato.Cuit(recibo.CuitApoderado ?? "")}");

        var periodoDesde = new DateOnly(recibo.PeriodoAnio, recibo.PeriodoMes, 1);
        var periodoHasta = periodoDesde.AddMonths(1).AddDays(-1);

        return new ComprobanteDocumento
        {
            CodigoTipo    = recibo.CodigoAfip,
            NombreOverride = "RECIBO",
            PuntoVenta    = recibo.PuntoDeVenta,
            Numero        = recibo.NumeroComprobante,
            FechaEmision  = recibo.FechaEmision,
            Cae           = recibo.CAE,
            FechaVencimientoCae = recibo.FechaVencimientoCAE,
            ImporteTotal  = recibo.Importe,
            ConceptoGeneral = vouchers.Count == 0 ? recibo.Detalle : null,
            Items         = items.Count > 0 ? items : [],
            Leyendas      = leyendas,
            PeriodoServicioDesde = periodoDesde,
            PeriodoServicioHasta = periodoHasta,
            FechaVencimientoPago = recibo.FechaVencimientoPago,
            Emisor  = BuildEmisor(config.CuitEmisor, recibo),
            Receptor = new ReceptorDocumento
            {
                RazonSocial   = receptorNombre,
                TipoDocumento = tipoDoc,
                NroDocumento  = nroDoc,
                Domicilio     = recibo.Agencia?.Domicilio,
                CondicionIva  = recibo.Agencia?.CondicionIva
            }
        };
    }

    private EmisorDocumento BuildEmisor(string cuitEmisorConfig, Recibo? recibo)
    {
        // Si el recibo fue emitido por un apoderado, el QR debe llevar el CUIT del apoderado.
        var (razonSocial, cuitStr) = recibo is { EsApoderado: true, NombreApoderado: { Length: > 0 } nombre }
            ? (nombre, recibo.CuitApoderado ?? cuitEmisorConfig)
            : (RazonSocialEmisor, cuitEmisorConfig);

        return new EmisorDocumento
        {
            RazonSocial    = razonSocial,
            Cuit           = Formato.ParseCuit(cuitStr),
            CondicionIva   = "IVA Exento",
            ColorAcentoHex = "#00695C",
            LogoPng        = LogoBytes is { Length: > 0 } ? LogoBytes : null
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

    private static void CuerpoVoucher(IContainer e, Voucher voucher)
    {
        e.Column(col =>
        {
            col.Item().Element(x => EncabezadoVoucher(x, voucher));

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

    private static void EncabezadoVoucher(IContainer e, Voucher voucher)
    {
        e.BorderBottom(2).BorderColor(_theme.Acento).PaddingBottom(8).Row(row =>
        {
            if (LogoBytes is { Length: > 0 })
                row.ConstantItem(58).AlignMiddle().Image(LogoBytes).FitWidth();
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
