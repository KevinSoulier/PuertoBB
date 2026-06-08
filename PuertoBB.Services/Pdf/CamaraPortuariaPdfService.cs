using Afip.Documentos;
using Afip.Documentos.Pdf;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Services.Common;

namespace PuertoBB.Services.Pdf;

public class CamaraPortuariaPdfService : ICamaraPortuariaPdfService
{
    private const string RazonSocialEmisor = "Cámara Portuaria de Bahía Blanca";

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
        var (tipoDoc, nroDoc) = Formato.ParseReceptorDoc(recibo.Empresa?.Cuit);
        var receptorNombre = recibo.Empresa?.RazonSocial is { Length: > 0 } rs
            ? rs
            : recibo.Empresa?.Nombre ?? $"#{recibo.EmpresaId}";

        var periodoDesde = new DateOnly(recibo.PeriodoAnio, recibo.PeriodoMes, 1);
        var periodoHasta = periodoDesde.AddMonths(1).AddDays(-1);

        var doc = new ComprobanteDocumento
        {
            CodigoTipo    = recibo.CodigoAfip,
            NombreOverride = "RECIBO",
            PuntoVenta    = recibo.PuntoDeVenta,
            Numero        = recibo.NumeroComprobante,
            FechaEmision  = recibo.FechaEmision,
            Cae           = recibo.CAE,
            FechaVencimientoCae = recibo.FechaVencimientoCAE,
            ImporteTotal  = recibo.Importe,
            ConceptoGeneral = recibo.Detalle,
            PeriodoServicioDesde = periodoDesde,
            PeriodoServicioHasta = periodoHasta,
            FechaVencimientoPago = recibo.FechaVencimientoPago,
            Emisor = new EmisorDocumento
            {
                RazonSocial   = RazonSocialEmisor,
                Cuit          = Formato.ParseCuit(config.CuitEmisor),
                CondicionIva  = "IVA Exento",
                ColorAcentoHex = "#1565C0"
            },
            Receptor = new ReceptorDocumento
            {
                RazonSocial   = receptorNombre,
                TipoDocumento = tipoDoc,
                NroDocumento  = nroDoc,
                Domicilio     = recibo.Empresa?.Domicilio,
                CondicionIva  = recibo.Empresa?.CondicionIva
            }
        };

        return _documentos.GenerarPdf(doc);
    }

    public async Task<byte[]> GenerarPdfNotaDeCreditoAsync(NotaDeCredito nc, CancellationToken ct = default)
    {
        var config = await _configProvider.GetAsync(ct);
        var recibo = nc.ReciboOriginal;
        var (tipoDoc, nroDoc) = Formato.ParseReceptorDoc(recibo?.Empresa?.Cuit);
        var receptorNombre = recibo?.Empresa?.RazonSocial is { Length: > 0 } rs
            ? rs
            : recibo?.Empresa?.Nombre ?? string.Empty;

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
            Emisor = new EmisorDocumento
            {
                RazonSocial   = RazonSocialEmisor,
                Cuit          = Formato.ParseCuit(config.CuitEmisor),
                CondicionIva  = "IVA Exento",
                ColorAcentoHex = "#1565C0"
            },
            Receptor = new ReceptorDocumento
            {
                RazonSocial  = receptorNombre,
                TipoDocumento = tipoDoc,
                NroDocumento  = nroDoc
            }
        };

        return _documentos.GenerarPdf(doc);
    }
}
