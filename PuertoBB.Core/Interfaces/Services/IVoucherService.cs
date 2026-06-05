using PuertoBB.Core.Common;
using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Core.Interfaces.Services;

/// <summary>Alta y consulta de vouchers del Centro Marítimo.</summary>
public interface IVoucherService
{
    /// <summary>Crea un voucher; asigna el número desde el contador y deriva el período de la fecha.</summary>
    Task<ServiceResult<Voucher>> CrearVoucherAsync(int agenciaId, int barcoId, DateTime fecha, decimal importe, CancellationToken ct = default);

    /// <summary>Vouchers pendientes de consolidar en un período.</summary>
    Task<ServiceResult<IReadOnlyList<Voucher>>> GetPendientesAsync(int anio, int mes, CancellationToken ct = default);

    /// <summary>Actualiza un voucher pendiente (no consolidado).</summary>
    Task<ServiceResult<bool>> ActualizarVoucherAsync(Voucher voucher, CancellationToken ct = default);

    /// <summary>Elimina un voucher pendiente (no consolidado).</summary>
    Task<ServiceResult<bool>> EliminarVoucherAsync(int voucherId, CancellationToken ct = default);
}
