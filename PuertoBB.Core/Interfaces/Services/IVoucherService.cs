using PuertoBB.Core.Common;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Models.Resultados;

namespace PuertoBB.Core.Interfaces.Services;

/// <summary>Alta y consulta de vouchers del Centro Marítimo.</summary>
public interface IVoucherService
{
    /// <summary>Crea un voucher; asigna el número desde el contador y deriva el período de la fecha.</summary>
    Task<ServiceResult<Voucher>> CrearVoucherAsync(int agenciaId, int barcoId, DateTime fecha, decimal importe, CancellationToken ct = default);

    /// <summary>Vouchers pendientes de consolidar en un período.</summary>
    Task<ServiceResult<IReadOnlyList<Voucher>>> GetPendientesAsync(int anio, int mes, CancellationToken ct = default);

    /// <summary>
    /// Todos los vouchers de un período (pendientes + consolidados), con su recibo cargado
    /// para mostrar el estado. El voucher queda como referencia aunque ya esté consolidado.
    /// </summary>
    Task<ServiceResult<IReadOnlyList<Voucher>>> GetDelPeriodoAsync(int anio, int mes, CancellationToken ct = default);

    /// <summary>Actualiza un voucher pendiente (no consolidado).</summary>
    Task<ServiceResult<bool>> ActualizarVoucherAsync(Voucher voucher, CancellationToken ct = default);

    /// <summary>Elimina un voucher pendiente (no consolidado).</summary>
    Task<ServiceResult<bool>> EliminarVoucherAsync(int voucherId, CancellationToken ct = default);

    /// <summary>
    /// Devuelve el shape de la pantalla de Cierre de Período: una fila por agencia que tuvo vouchers
    /// en el período, con sus vouchers, total y estado del recibo consolidado (si existe).
    /// </summary>
    Task<ServiceResult<IReadOnlyList<AgenciaCierrePeriodoVm>>> GetCierrePeriodoAsync(int anio, int mes, CancellationToken ct = default);
}
