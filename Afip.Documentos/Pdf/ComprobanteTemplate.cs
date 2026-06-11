using Afip.Documentos.Catalogo;
using Afip.Documentos.Qr;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Afip.Documentos.Pdf;

/// <summary>
/// Template de comprobante AFIP con el estilo oficial de "Comprobantes en Línea":
/// layout formal en blanco/negro/gris, sin colores de empresa, con bordes de sección.
/// </summary>
internal class ComprobanteTemplate : IDocument
{
    private readonly ComprobanteDocumento _doc;

    // Paleta monocromática estilo AFIP oficial
    private static readonly string Negro       = "#1A1A1A";
    private static readonly string Gris        = "#555555";
    private static readonly string BordeGris   = "#CCCCCC";
    private static readonly string FondoGris   = "#EEEEEE";

    public ComprobanteTemplate(ComprobanteDocumento doc) => _doc = doc;

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(p =>
        {
            p.Size(PageSizes.A4);
            p.Margin(40);
            p.DefaultTextStyle(s => s.FontFamily(Fonts.Calibri).FontSize(9).FontColor(Negro));

            p.Header().Element(Encabezado);
            p.Content().Element(Cuerpo);
            p.Footer().Element(Pie);
        });
    }

    // ── 1. ENCABEZADO — 3 columnas: emisor | letra | datos comprobante ─────────

    private void Encabezado(IContainer e)
    {
        var letra = _doc.LetraOverride ?? TipoComprobanteAfip.Letra(_doc.CodigoTipo);
        var nombre = _doc.NombreOverride ?? TipoComprobanteAfip.Nombre(_doc.CodigoTipo);

        e.Border(1).BorderColor(BordeGris).Row(row =>
        {
            // Columna izquierda: nombre/fantasía, domicilio y condición IVA del emisor
            row.RelativeItem().Padding(8).Column(col =>
            {
                if (_doc.Emisor.LogoPng is { Length: > 0 })
                    col.Item().Height(45).AlignLeft().Image(_doc.Emisor.LogoPng).FitHeight();

                col.Item().Text(_doc.Emisor.RazonSocial).FontSize(11).Bold().FontColor(Negro);

                col.Item().PaddingTop(2).Text(t =>
                {
                    t.Span("Razón Social: ").FontColor(Gris);
                    t.Span(_doc.Emisor.RazonSocial);
                });

                if (!string.IsNullOrWhiteSpace(_doc.Emisor.Domicilio))
                    col.Item().Text(t =>
                    {
                        t.Span("Domicilio Comercial: ").FontColor(Gris);
                        t.Span(_doc.Emisor.Domicilio);
                    });

                if (!string.IsNullOrWhiteSpace(_doc.Emisor.CondicionIva))
                    col.Item().Text(t =>
                    {
                        t.Span("Condición frente al IVA: ").FontColor(Gris);
                        t.Span(_doc.Emisor.CondicionIva);
                    });
            });

            // Columna central: recuadro con letra y código (obligatorio por norma AFIP)
            row.ConstantItem(54)
               .BorderLeft(1).BorderRight(1).BorderColor(BordeGris)
               .AlignCenter().AlignMiddle()
               .Column(col =>
               {
                   col.Item().AlignCenter().Text(letra)
                      .FontSize(30).Bold().FontColor(Negro);
                   col.Item().AlignCenter().Text($"COD.\n{_doc.CodigoTipo:00}")
                      .FontSize(6).FontColor(Gris).Italic();
               });

            // Columna derecha: nombre comprobante, PV/Nro/Fecha, y datos fiscales del emisor
            row.ConstantItem(200).Padding(8).Column(col =>
            {
                col.Item().AlignCenter().Text(nombre)
                   .FontSize(13).Bold().FontColor(Negro);

                col.Item().PaddingTop(4).BorderTop(1).BorderColor(BordeGris).PaddingTop(4);

                // Punto de Venta y Comp. Nro en la misma línea (norma AFIP)
                col.Item().Row(r =>
                {
                    r.RelativeItem().Text(t =>
                    {
                        t.Span("Punto de Venta: ").FontColor(Gris);
                        t.Span($"{_doc.PuntoVenta:0000}").Bold();
                    });
                    r.RelativeItem().Text(t =>
                    {
                        t.Span("Comp. Nro: ").FontColor(Gris);
                        t.Span($"{_doc.Numero:00000000}").Bold();
                    });
                });

                FilaDato(col, "Fecha de Emisión:", _doc.FechaEmision.ToString("dd/MM/yyyy"));

                // Datos fiscales del emisor (CUIT, Ingresos Brutos, Inicio Actividades)
                col.Item().PaddingTop(4).BorderTop(1).BorderColor(BordeGris).PaddingTop(4);

                col.Item().Text(t =>
                {
                    t.Span("CUIT: ").FontColor(Gris);
                    t.Span(FormatCuit(_doc.Emisor.Cuit)).Bold();
                });

                if (!string.IsNullOrWhiteSpace(_doc.Emisor.IngresosBrutos))
                    col.Item().Text(t =>
                    {
                        t.Span("Ingresos Brutos: ").FontColor(Gris);
                        t.Span(_doc.Emisor.IngresosBrutos).Bold();
                    });

                if (_doc.Emisor.InicioActividades.HasValue)
                    col.Item().Text(t =>
                    {
                        t.Span("Fecha de Inicio de Actividades: ").FontColor(Gris);
                        t.Span(_doc.Emisor.InicioActividades.Value.ToString("dd/MM/yyyy")).Bold();
                    });
            });
        });
    }

    // ── 2. CUERPO ────────────────────────────────────────────────────────────

    private void Cuerpo(IContainer e)
    {
        e.Column(col =>
        {
            col.Spacing(0);

            // Banda de período / vencimiento pago
            if (_doc.PeriodoServicioDesde.HasValue || _doc.FechaVencimientoPago.HasValue)
                col.Item().Element(BandaPeriodo);

            // Sección datos del receptor
            col.Item().PaddingTop(6).Element(SeccionReceptor);

            // Comprobante asociado (NC que cancela un comprobante previo) — P1-7
            if (_doc.ComprobanteAsociado is { } ca)
                col.Item().PaddingTop(4).Text(t =>
                {
                    t.Span("Comprobante asociado: ").SemiBold();
                    t.Span($"{TipoComprobanteAfip.Nombre(ca.CodigoTipo)} {TipoComprobanteAfip.Letra(ca.CodigoTipo)} — {ca.PuntoVenta:0000}-{ca.Numero:00000000}");
                });

            // Tabla de items o texto libre
            col.Item().PaddingTop(6).Element(Detalle);

            // Observaciones / leyendas
            if (_doc.Leyendas.Count > 0)
                col.Item().PaddingTop(4).Element(SeccionLeyendas);
        });
    }

    private void BandaPeriodo(IContainer e)
    {
        e.PaddingTop(4).Border(1).BorderColor(BordeGris).Padding(5).Row(row =>
        {
            row.RelativeItem().Text(t =>
            {
                if (_doc.PeriodoServicioDesde.HasValue && _doc.PeriodoServicioHasta.HasValue)
                {
                    t.Span("Período Facturado Desde: ").FontColor(Gris);
                    t.Span(_doc.PeriodoServicioDesde.Value.ToString("dd/MM/yyyy"));
                    t.Span("   Hasta: ").FontColor(Gris);
                    t.Span(_doc.PeriodoServicioHasta.Value.ToString("dd/MM/yyyy"));
                }
            });

            if (_doc.FechaVencimientoPago.HasValue)
                row.AutoItem().Text(t =>
                {
                    t.Span("   Fecha de Vto. para el pago: ").FontColor(Gris);
                    t.Span(_doc.FechaVencimientoPago.Value.ToString("dd/MM/yyyy")).Bold();
                });
        });
    }

    private void SeccionReceptor(IContainer e)
    {
        e.Border(1).BorderColor(BordeGris).Padding(6).Column(c =>
        {
            c.Spacing(2);

            // Fila 1: CUIT a la izquierda, Razón Social a la derecha (estilo ARCA)
            c.Item().Row(r =>
            {
                r.ConstantItem(170).Text(t =>
                {
                    var docLabel = _doc.Receptor.TipoDocumento switch
                    {
                        80 => "CUIT",
                        96 => "DNI/CUIL",
                        99 => string.Empty,
                        _  => $"Doc. {_doc.Receptor.TipoDocumento}"
                    };
                    if (docLabel.Length > 0)
                    {
                        t.Span($"{docLabel}: ").FontColor(Gris);
                        t.Span(_doc.Receptor.TipoDocumento == 80
                            ? FormatCuit(_doc.Receptor.NroDocumento)
                            : _doc.Receptor.NroDocumento.ToString()).Bold();
                    }
                    else
                    {
                        t.Span("Consumidor Final").FontColor(Gris);
                    }
                });
                r.RelativeItem().Text(t =>
                {
                    t.Span("Apellido y Nombre / Razón Social: ").FontColor(Gris);
                    t.Span(_doc.Receptor.RazonSocial).Bold();
                });
            });

            // Fila 2: Condición IVA y Domicilio Comercial
            c.Item().Row(r =>
            {
                r.RelativeItem().Text(t =>
                {
                    if (!string.IsNullOrWhiteSpace(_doc.Receptor.CondicionIva))
                    {
                        t.Span("Condición frente al IVA: ").FontColor(Gris);
                        t.Span(_doc.Receptor.CondicionIva);
                    }
                });
                if (!string.IsNullOrWhiteSpace(_doc.Receptor.Domicilio))
                    r.ConstantItem(240).Text(t =>
                    {
                        t.Span("Domicilio Comercial: ").FontColor(Gris);
                        t.Span(_doc.Receptor.Domicilio);
                    });
            });

            // Fila 3: Condición de venta (opcional)
            if (!string.IsNullOrWhiteSpace(_doc.CondicionVenta))
                c.Item().Text(t =>
                {
                    t.Span("Condición de venta: ").FontColor(Gris);
                    t.Span(_doc.CondicionVenta);
                });
        });
    }

    private void Detalle(IContainer e)
    {
        if (_doc.Items.Count > 0)
            TablaItems(e);
        else
            DetalleTextoLibre(e);
    }

    private void DetalleTextoLibre(IContainer e)
    {
        var esRecibo = string.Equals(_doc.NombreOverride, "RECIBO", StringComparison.OrdinalIgnoreCase);

        if (esRecibo)
        {
            // Formato recibo ARCA: "Recibi(mos) la suma de: $X / en concepto de: descripción"
            e.Column(col =>
            {
                col.Spacing(4);
                col.Item().Text(t =>
                {
                    t.Span("Recibi(mos) la suma de:   ").FontColor(Gris);
                    t.Span(FormatMoneda(_doc.ImporteTotal)).Bold().FontSize(11);
                });
                col.Item().Text("en concepto de:").FontColor(Gris);
                col.Item().Text(_doc.ConceptoGeneral ?? string.Empty);
            });
        }
        else
        {
            // Tabla genérica para NC y otros comprobantes sin items
            e.Border(1).BorderColor(BordeGris).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn();
                    c.ConstantColumn(120);
                });

                table.Header(h =>
                {
                    h.Cell().Background(FondoGris).Padding(4).Text("Descripción").Bold().FontSize(8);
                    h.Cell().Background(FondoGris).Padding(4).AlignRight().Text("Importe").Bold().FontSize(8);
                });

                table.Cell().Padding(5).Text(_doc.ConceptoGeneral ?? string.Empty);
                table.Cell().Padding(5).AlignRight().Text(FormatMoneda(_doc.ImporteTotal)).Bold();
            });
        }
    }

    private void TablaItems(IContainer e)
    {
        e.Border(1).BorderColor(BordeGris).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(3);    // Descripción
                c.ConstantColumn(50);   // Cant.
                c.ConstantColumn(90);   // P. Unit.
                c.ConstantColumn(90);   // Subtotal
            });

            table.Header(h =>
            {
                CeldaHeader(h.Cell(), "Descripción");
                CeldaHeader(h.Cell(), "Cant.", align: true);
                CeldaHeader(h.Cell(), "Precio Unit.", align: true);
                CeldaHeader(h.Cell(), "Total", align: true);
            });

            foreach (var item in _doc.Items)
            {
                table.Cell().Element(Celda).Text(item.Descripcion);
                table.Cell().Element(Celda).AlignRight().Text(item.Cantidad.ToString("G"));
                table.Cell().Element(Celda).AlignRight().Text(FormatMoneda(item.PrecioUnitario));
                table.Cell().Element(Celda).AlignRight().Text(FormatMoneda(item.Subtotal));
            }
        });
    }

    private void SeccionLeyendas(IContainer e)
    {
        e.Column(col =>
        {
            col.Item().Text("Observaciones:").FontColor(Gris).FontSize(8);
            foreach (var leyenda in _doc.Leyendas)
                col.Item().PaddingLeft(8).Text(leyenda).FontSize(8).FontColor(Negro);
        });
    }

    private void Totales(IContainer e)
    {
        var tieneDesglose = _doc.ImporteNeto.HasValue || _doc.ImporteIva.HasValue || _doc.ImporteExento.HasValue;
        var subtotal = tieneDesglose
            ? _doc.ImporteNeto.GetValueOrDefault() + _doc.ImporteIva.GetValueOrDefault() + _doc.ImporteExento.GetValueOrDefault()
            : _doc.ImporteTotal;
        var otrosTributos = _doc.ImporteTotal - subtotal;

        e.AlignRight().Column(col =>
        {
            col.Spacing(2);

            // Subtotal: solo si hay desglose de IVA/Neto/Exento
            if (tieneDesglose)
                FilaDatoTotal(col, "Subtotal:", FormatMoneda(subtotal));

            // Otros Tributos: solo si tienen valor
            if (otrosTributos > 0)
                FilaDatoTotal(col, "Importe Otros Tributos:", FormatMoneda(otrosTributos));

            // Total: siempre
            col.Item().BorderTop(1).BorderColor(BordeGris).PaddingTop(2).Row(r =>
            {
                r.RelativeItem().AlignRight().Text("Importe Total: ").SemiBold().FontSize(10);
                r.ConstantItem(90).AlignRight().Text(FormatMoneda(_doc.ImporteTotal)).Bold().FontSize(10);
            });
        });
    }

    // ── 3. PIE — Totales + QR | ARCA texto | CAE ────────────────────────────

    /// <summary>Footer completo: Totales arriba + recuadro QR+CAE abajo.</summary>
    private void Pie(IContainer e)
    {
        e.Column(col =>
        {
            col.Spacing(0);
            col.Item().PaddingBottom(6).Element(Totales);
            col.Item().Element(PieCae);
        });
    }

    private void PieCae(IContainer e)
    {
        byte[]? qrPng = null;
        try
        {
            var payload = new AfipQrPayload(
                CuitEmisor: _doc.Emisor.Cuit,
                PuntoVenta: _doc.PuntoVenta,
                TipoComprobante: _doc.CodigoTipo,
                NumeroComprobante: _doc.Numero,
                Importe: _doc.ImporteTotal,
                TipoDocReceptor: _doc.Receptor.TipoDocumento,
                NroDocReceptor: _doc.Receptor.NroDocumento,
                CodAutorizacion: long.TryParse(_doc.Cae, out var caeL) ? caeL : 0,
                FechaComprobante: DateOnly.FromDateTime(_doc.FechaEmision)
            );
            qrPng = AfipQrBuilder.GenerarPng(payload);
        }
        catch { /* si falla el QR, el pie muestra solo el texto */ }

        e.Border(1).BorderColor(BordeGris).Padding(6).Row(row =>
        {
            // QR a la izquierda (estilo ARCA)
            if (qrPng is { Length: > 0 })
                row.ConstantItem(68).AlignLeft().AlignMiddle().Image(qrPng).FitWidth();
            else
                row.ConstantItem(68);

            // ARCA: agencia + texto de comprobante autorizado
            row.RelativeItem().PaddingHorizontal(8).AlignMiddle().Column(col =>
            {
                col.Item().Text("ARCA").Bold().FontSize(10).FontColor(Negro);
                col.Item().Text("Agencia de Recaudación y Control Aduanero")
                   .FontSize(7).FontColor(Gris);
                col.Item().PaddingTop(2).Text("Comprobante Autorizado")
                   .FontSize(8).SemiBold().FontColor(Negro);
                col.Item().Text("Esta Agencia no se responsabiliza por los datos ingresados en el detalle de la operación")
                   .FontSize(7).FontColor(Gris).Italic();
            });

            // CAE y fecha de vencimiento a la derecha
            row.ConstantItem(185).AlignMiddle().Column(col =>
            {
                col.Item().Text(t =>
                {
                    t.Span("CAE Nº: ").FontColor(Gris);
                    t.Span(_doc.Cae).Bold();
                });
                col.Item().Text(t =>
                {
                    t.Span("Fecha de Vto. de CAE: ").FontColor(Gris);
                    t.Span(_doc.FechaVencimientoCae.ToString("dd/MM/yyyy")).Bold();
                });
            });
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void CeldaHeader(IContainer e, string texto, bool align = false)
    {
        var cell = e.Background(FondoGris).Padding(4);
        if (align) cell.AlignRight().Text(texto).Bold().FontSize(8).FontColor(Negro);
        else       cell.Text(texto).Bold().FontSize(8).FontColor(Negro);
    }

    private IContainer Celda(IContainer e)
        => e.BorderBottom(1).BorderColor(BordeGris).PaddingVertical(4).PaddingHorizontal(5);

    private static void FilaDato(ColumnDescriptor col, string label, string valor)
        => col.Item().Row(r =>
        {
            r.ConstantItem(110).Text(label).FontColor(Gris);
            r.RelativeItem().Text(valor).Bold();
        });

    private static void FilaDatoTotal(ColumnDescriptor col, string label, string valor)
        => col.Item().Row(r =>
        {
            r.RelativeItem().AlignRight().Text(label).FontColor(Gris);
            r.ConstantItem(90).AlignRight().Text(valor);
        });

    private static string FormatMoneda(decimal v)
        => v.ToString("C2", new System.Globalization.CultureInfo("es-AR"));

    private static string FormatMonedaSinSimbolo(decimal v)
        => v.ToString("N2", new System.Globalization.CultureInfo("es-AR"));

    private static string FormatCuit(long cuit)
    {
        var s = cuit.ToString("D11");
        return s.Length == 11 ? $"{s[..2]}-{s[2..10]}-{s[10]}" : s;
    }
}
