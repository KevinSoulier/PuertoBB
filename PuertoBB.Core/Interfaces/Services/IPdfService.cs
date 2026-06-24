using CentroMaritimo = PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Core.Interfaces.Services;

/// <summary>
/// PDF de la Cámara Portuaria. Separado del Centro Marítimo porque los documentos difieren.
/// </summary>
public interface ICamaraPortuariaPdfService
{
    Task<byte[]> GenerarPdfReciboAsync(Recibo recibo, CancellationToken ct = default);
    Task<byte[]> GenerarPdfNotaDeCreditoAsync(NotaDeCredito nc, CancellationToken ct = default);
}

/// <summary>
/// PDF del Centro Marítimo. Incluye voucher individual y recibo consolidado (recibo + vouchers).
/// </summary>
public interface ICentroMaritimoPdfService
{
    Task<byte[]> GenerarPdfVoucherAsync(CentroMaritimo.Voucher voucher, CancellationToken ct = default);
    Task<byte[]> GenerarPdfConsolidadoAsync(Recibo recibo, IEnumerable<CentroMaritimo.Voucher> vouchers, CancellationToken ct = default);
    Task<byte[]> GenerarPdfReciboAsync(Recibo recibo, CancellationToken ct = default);
    Task<byte[]> GenerarPdfNotaDeCreditoAsync(NotaDeCredito nc, CancellationToken ct = default);

    /// <summary>
    /// PDF único de descarga: si <paramref name="recibo"/> no es null, concatena PDF del recibo
    /// (con CAE+QR) seguido de los PDFs individuales de cada voucher. Si es null, concatena solo
    /// los PDFs de vouchers (vista previa antes de cerrar el período).
    /// </summary>
    Task<byte[]> GenerarPdfDescargaAsync(
        IReadOnlyList<CentroMaritimo.Voucher> vouchers,
        Recibo? recibo,
        CancellationToken ct = default);
}

/// <summary>Concatena múltiples PDFs en uno solo, preservando el orden.</summary>
public interface IPdfMerger
{
    byte[] Merge(IEnumerable<byte[]> pdfs);
}
