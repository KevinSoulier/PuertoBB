using CamaraPortuaria = PuertoBB.Core.Entities.CamaraPortuaria;
using CentroMaritimo = PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Core.Interfaces.Services;

/// <summary>
/// PDF de la Cámara Portuaria. Separado del Centro Marítimo porque los documentos difieren.
/// </summary>
public interface ICamaraPortuariaPdfService
{
    Task<byte[]> GenerarPdfReciboAsync(CamaraPortuaria.Recibo recibo, CancellationToken ct = default);
    Task<byte[]> GenerarPdfNotaDeCreditoAsync(CamaraPortuaria.NotaDeCredito nc, CancellationToken ct = default);
}

/// <summary>
/// PDF del Centro Marítimo. Incluye voucher individual y recibo consolidado (recibo + vouchers).
/// </summary>
public interface ICentroMaritimoPdfService
{
    Task<byte[]> GenerarPdfVoucherAsync(CentroMaritimo.Voucher voucher, CancellationToken ct = default);
    Task<byte[]> GenerarPdfConsolidadoAsync(CentroMaritimo.Recibo recibo, IEnumerable<CentroMaritimo.Voucher> vouchers, CancellationToken ct = default);
    Task<byte[]> GenerarPdfReciboAsync(CentroMaritimo.Recibo recibo, CancellationToken ct = default);
    Task<byte[]> GenerarPdfNotaDeCreditoAsync(CentroMaritimo.NotaDeCredito nc, CancellationToken ct = default);
}
