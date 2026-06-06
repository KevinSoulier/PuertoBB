using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Services.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PuertoBB.Services.Pdf;

public class CentroMaritimoPdfService : ICentroMaritimoPdfService
{
    private readonly PdfTheme _theme = PdfTheme.CentroMaritimo;
    private const string Titulo = "Centro Marítimo";

    public Task<byte[]> GenerarPdfReciboAsync(Recibo recibo, CancellationToken ct = default)
    {
        var bytes = Document.Create(c => c.Page(p =>
        {
            ConfigurarPagina(p);
            p.Header().Element(e => Encabezado(e, "RECIBO", recibo.CodigoAfip, recibo.PuntoDeVenta, recibo.NumeroComprobante));
            p.Content().Element(e => CuerpoRecibo(e, recibo));
            p.Footer().Element(e => PieCae(e, recibo.CAE, recibo.FechaVencimientoCAE));
        })).GeneratePdf();
        return Task.FromResult(bytes);
    }

    public Task<byte[]> GenerarPdfConsolidadoAsync(Recibo recibo, IEnumerable<Voucher> vouchers, CancellationToken ct = default)
    {
        var lista = vouchers.OrderBy(v => v.Numero).ToList();
        var bytes = Document.Create(c => c.Page(p =>
        {
            ConfigurarPagina(p);
            p.Header().Element(e => Encabezado(e, "RECIBO CONSOLIDADO", recibo.CodigoAfip, recibo.PuntoDeVenta, recibo.NumeroComprobante));
            p.Content().Element(e =>
            {
                e.Column(col =>
                {
                    col.Item().Element(x => CuerpoRecibo(x, recibo));
                    col.Item().PaddingTop(16).Text("Detalle de vouchers").SemiBold().FontColor(_theme.Acento);
                    col.Item().PaddingTop(6).Element(x => TablaVouchers(x, lista));
                });
            });
            p.Footer().Element(e => PieCae(e, recibo.CAE, recibo.FechaVencimientoCAE));
        })).GeneratePdf();
        return Task.FromResult(bytes);
    }

    public Task<byte[]> GenerarPdfVoucherAsync(Voucher voucher, CancellationToken ct = default)
    {
        var bytes = Document.Create(c => c.Page(p =>
        {
            ConfigurarPagina(p);
            p.Header().BorderBottom(2).BorderColor(_theme.Acento).PaddingBottom(8)
                .Text($"VOUCHER Nro. {voucher.Numero}").FontSize(16).Bold().FontColor(_theme.Acento);
            p.Content().PaddingVertical(16).Column(col =>
            {
                col.Spacing(6);
                col.Item().Text(t => { t.Span("Agencia: ").SemiBold(); t.Span(voucher.Agencia?.Nombre ?? $"#{voucher.AgenciaId}"); });
                col.Item().Text(t => { t.Span("Barco: ").SemiBold(); t.Span(voucher.Barco?.Nombre ?? $"#{voucher.BarcoId}"); });
                col.Item().Text(t => { t.Span("Fecha de ingreso: ").SemiBold(); t.Span(Formato.Fecha(voucher.Fecha)); });
                col.Item().PaddingTop(10).Background("#F5F5F5").Padding(12).Row(r =>
                {
                    r.RelativeItem().Text("Importe").SemiBold();
                    r.ConstantItem(160).AlignRight().Text(Formato.Moneda(voucher.Importe)).FontSize(15).Bold().FontColor(_theme.Acento);
                });
            });
        })).GeneratePdf();
        return Task.FromResult(bytes);
    }

    public Task<byte[]> GenerarPdfNotaDeCreditoAsync(NotaDeCredito nc, CancellationToken ct = default)
    {
        var bytes = Document.Create(c => c.Page(p =>
        {
            ConfigurarPagina(p);
            p.Header().Element(e => Encabezado(e, "NOTA DE CRÉDITO", nc.CodigoAfip, nc.PuntoDeVenta, nc.NumeroComprobante));
            p.Content().PaddingVertical(20).Column(col =>
            {
                col.Item().Text($"Anula el recibo original Nro. {nc.ReciboOriginalId}.");
                col.Item().PaddingTop(8).Text($"Fecha de emisión: {Formato.Fecha(nc.FechaEmision)}").FontColor(_theme.TextoSecundario);
            });
            p.Footer().Element(e => PieCae(e, nc.CAE, nc.FechaVencimientoCAE));
        })).GeneratePdf();
        return Task.FromResult(bytes);
    }

    private void ConfigurarPagina(PageDescriptor p)
    {
        p.Size(PageSizes.A4);
        p.Margin(40);
        p.DefaultTextStyle(s => s.FontFamily(_theme.Fuente).FontSize(10).FontColor(_theme.Texto));
    }

    private void Encabezado(IContainer e, string tipo, int codigoAfip, int puntoVenta, long numero)
    {
        e.BorderBottom(2).BorderColor(_theme.Acento).PaddingBottom(8).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(Titulo).FontSize(16).Bold().FontColor(_theme.Acento);
                col.Item().Text("Comprobante Exento de IVA").FontSize(9).FontColor(_theme.TextoSecundario);
            });
            row.ConstantItem(190).Column(col =>
            {
                col.Item().AlignRight().Text(tipo).FontSize(13).Bold();
                col.Item().AlignRight().Text($"Cód. AFIP {codigoAfip:00}").FontSize(9).FontColor(_theme.TextoSecundario);
                col.Item().AlignRight().Text($"Nro. {puntoVenta:0000}-{numero:00000000}").FontSize(11).SemiBold();
            });
        });
    }

    private void CuerpoRecibo(IContainer e, Recibo recibo)
    {
        e.PaddingVertical(16).Column(col =>
        {
            col.Spacing(6);
            col.Item().Row(r =>
            {
                r.RelativeItem().Text(t => { t.Span("Agencia: ").SemiBold(); t.Span(recibo.Agencia?.Nombre ?? $"#{recibo.AgenciaId}"); });
                r.ConstantItem(220).AlignRight().Text(t => { t.Span("Fecha: ").SemiBold(); t.Span(Formato.Fecha(recibo.FechaEmision)); });
            });
            if (recibo.Agencia is not null)
                col.Item().Text(t => { t.Span("CUIT: ").SemiBold(); t.Span(Formato.Cuit(recibo.Agencia.Cuit)); });

            if (recibo.EsApoderado && !string.IsNullOrWhiteSpace(recibo.NombreApoderado))
                col.Item().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontColor(_theme.TextoSecundario));
                    t.Span("Emite (apoderado): ").SemiBold();
                    t.Span($"{recibo.NombreApoderado} — CUIT {Formato.Cuit(recibo.CuitApoderado ?? "")}");
                });

            col.Item().Text(t => { t.Span("Período: ").SemiBold(); t.Span(Formato.Periodo(recibo.PeriodoAnio, recibo.PeriodoMes)); });

            col.Item().PaddingTop(12).Background("#F5F5F5").Padding(12).Row(r =>
            {
                r.RelativeItem().Text(recibo.Detalle).FontColor(_theme.Texto);
                r.ConstantItem(160).AlignRight().Text(Formato.Moneda(recibo.Importe)).FontSize(16).Bold().FontColor(_theme.Acento);
            });

            col.Item().PaddingTop(8).Text(t =>
            {
                t.Span("Vencimiento del pago: ").SemiBold();
                t.Span(Formato.Fecha(recibo.FechaVencimientoPago));
            });
        });
    }

    private void TablaVouchers(IContainer e, IReadOnlyList<Voucher> vouchers)
    {
        e.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(60);   // Nro
                c.RelativeColumn();     // Barco
                c.ConstantColumn(90);   // Fecha
                c.ConstantColumn(110);  // Importe
            });

            table.Header(h =>
            {
                CeldaHeader(h.Cell(), "Nro.");
                CeldaHeader(h.Cell(), "Barco");
                CeldaHeader(h.Cell(), "Fecha");
                CeldaHeader(h.Cell(), "Importe");
            });

            foreach (var v in vouchers)
            {
                table.Cell().Element(Celda).Text(v.Numero.ToString());
                table.Cell().Element(Celda).Text(v.Barco?.Nombre ?? $"#{v.BarcoId}");
                table.Cell().Element(Celda).Text(Formato.Fecha(v.Fecha));
                table.Cell().Element(Celda).AlignRight().Text(Formato.Moneda(v.Importe));
            }

            table.Cell().ColumnSpan(3).Element(Celda).AlignRight().Text("Total").SemiBold();
            table.Cell().Element(Celda).AlignRight().Text(Formato.Moneda(vouchers.Sum(v => v.Importe))).Bold().FontColor(_theme.Acento);
        });
    }

    private void CeldaHeader(IContainer e, string texto)
        => e.Background(_theme.Acento).Padding(5).Text(texto).FontColor(Colors.White).SemiBold();

    private IContainer Celda(IContainer e)
        => e.BorderBottom(1).BorderColor(_theme.Borde).PaddingVertical(4).PaddingHorizontal(5);

    private void PieCae(IContainer e, string cae, DateTime vto)
    {
        e.BorderTop(1).BorderColor(_theme.Borde).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text(t => { t.DefaultTextStyle(s => s.FontSize(9)); t.Span("CAE: ").SemiBold(); t.Span(cae); });
            row.RelativeItem().AlignRight().Text(t => { t.DefaultTextStyle(s => s.FontSize(9)); t.Span("Vto. CAE: ").SemiBold(); t.Span(Formato.Fecha(vto)); });
        });
    }
}
