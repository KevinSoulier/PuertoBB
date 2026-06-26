using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;

public interface IVoucherRepository : IRepository<Voucher>
{
    /// <summary>Vouchers sin consolidar en el período (ConsolidacionId IS NULL), con Cliente y Barco.</summary>
    Task<IReadOnlyList<Voucher>> GetPendientesByPeriodoAsync(int anio, int mes, CancellationToken ct = default);

    /// <summary>Todos los vouchers del período (libres o consolidados), incluyendo Cliente, Barco y Consolidacion.Recibo.</summary>
    Task<IReadOnlyList<Voucher>> GetTodosByPeriodoAsync(int anio, int mes, CancellationToken ct = default);

    /// <summary>Clientes distintas que tienen vouchers pendientes en el período.</summary>
    Task<IReadOnlyList<Cliente>> GetClientesConVouchersPendientesAsync(int anio, int mes, CancellationToken ct = default);

    /// <summary>Asigna ConsolidacionId a un conjunto de vouchers (los agrega a una consolidación existente).</summary>
    Task MarcarConsolidadosAsync(IEnumerable<int> voucherIds, int consolidacionId, CancellationToken ct = default);

    /// <summary>Vouchers de una agencia (con Barco), opcionalmente filtrados por período.</summary>
    Task<IReadOnlyList<Voucher>> GetPorClienteAsync(int agenciaId, int? anio = null, int? mes = null, CancellationToken ct = default);

    /// <summary>Devuelve el voucher con Cliente y Barco cargados (para generar PDF individual).</summary>
    Task<Voucher?> GetByIdConDetalleAsync(int id, CancellationToken ct = default);

    /// <summary>Devuelve el voucher con Cliente, Barco y Consolidacion.Recibo cargados (para decidir si
    /// está libre, pendiente o emitido en la emisión individual por voucher).</summary>
    Task<Voucher?> GetByIdConEstadoAsync(int id, CancellationToken ct = default);
}
