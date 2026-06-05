using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Services.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PuertoBB.Services.Pdf;

public class CamaraPortuariaPdfService : ICamaraPortuariaPdfService
{
    private readonly PdfTheme _theme = PdfTheme.CamaraPortuaria;
    private const string Titulo = "Cámara Portuaria de Bahía Blanca";

    public Task<byte[]> GenerarPdfReciboAsync(Recibo recibo, CancellationToken ct = default)
    {
        var bytes = Document.Create(c => c.Page(p =>
        {
            ConfigurarPagina(p);
            p.Header().Element(e => Encabezado(e, "RECIBO", recibo.CodigoAfip, recibo.PuntoDeVenta, recibo.NumeroComprobante));
            p.Content().Element(e => Cuerpo(e, recibo));
            p.Footer().Element(e => PieCae(e, recibo.CAE, recibo.FechaVencimientoCAE));
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
                col.Item().Text($"Anula el recibo original Nro. {nc.ReciboOriginalId}.").FontColor(_theme.Texto);
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
            row.ConstantItem(180).Column(col =>
            {
                col.Item().AlignRight().Text(tipo).FontSize(14).Bold();
                col.Item().AlignRight().Text($"Cód. AFIP {codigoAfip:00}").FontSize(9).FontColor(_theme.TextoSecundario);
                col.Item().AlignRight().Text($"Nro. {puntoVenta:0000}-{numero:00000000}").FontSize(11).SemiBold();
            });
        });
    }

    private void Cuerpo(IContainer e, Recibo recibo)
    {
        e.PaddingVertical(16).Column(col =>
        {
            col.Spacing(6);
            col.Item().Row(r =>
            {
                r.RelativeItem().Text(t => { t.Span("Empresa: ").SemiBold(); t.Span(recibo.Empresa?.Nombre ?? $"#{recibo.EmpresaId}"); });
                r.ConstantItem(220).AlignRight().Text(t => { t.Span("Fecha: ").SemiBold(); t.Span(Formato.Fecha(recibo.FechaEmision)); });
            });
            if (recibo.Empresa is not null)
            {
                col.Item().Text(t => { t.Span("CUIT: ").SemiBold(); t.Span(Formato.Cuit(recibo.Empresa.Cuit)); });
                if (!string.IsNullOrWhiteSpace(recibo.Empresa.Domicilio))
                    col.Item().Text(t => { t.Span("Domicilio: ").SemiBold(); t.Span(recibo.Empresa.Domicilio); });
            }
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

    private void PieCae(IContainer e, string cae, DateTime vto)
    {
        e.BorderTop(1).BorderColor(_theme.Borde).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text(t => { t.DefaultTextStyle(s => s.FontSize(9)); t.Span("CAE: ").SemiBold(); t.Span(cae); });
            row.RelativeItem().AlignRight().Text(t => { t.DefaultTextStyle(s => s.FontSize(9)); t.Span("Vto. CAE: ").SemiBold(); t.Span(Formato.Fecha(vto)); });
        });
    }
}
