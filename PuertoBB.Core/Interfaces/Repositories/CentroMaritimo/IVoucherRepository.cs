using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;

public interface IVoucherRepository : IRepository<Voucher>
{
    /// <summary>Vouchers sin consolidar en el período (ReciboId IS NULL), con Cliente y Barco.</summary>
    Task<IReadOnlyList<Voucher>> GetPendientesByPeriodoAsync(int anio, int mes, CancellationToken ct = default);

    /// <summary>Todos los vouchers del período (con o sin Recibo), incluyendo Cliente, Barco y Recibo.</summary>
    Task<IReadOnlyList<Voucher>> GetTodosByPeriodoAsync(int anio, int mes, CancellationToken ct = default);

    /// <summary>Clientes distintas que tienen vouchers pendientes en el período.</summary>
    Task<IReadOnlyList<Cliente>> GetClientesConVouchersPendientesAsync(int anio, int mes, CancellationToken ct = default);

    /// <summary>Asigna ReciboId a un conjunto de vouchers (consolidación).</summary>
    Task MarcarConsolidadosAsync(IEnumerable<int> voucherIds, int reciboId, CancellationToken ct = default);

    /// <summary>Vouchers de una agencia (con Barco), opcionalmente filtrados por período.</summary>
    Task<IReadOnlyList<Voucher>> GetPorClienteAsync(int agenciaId, int? anio = null, int? mes = null, CancellationToken ct = default);

    /// <summary>Devuelve el voucher con Cliente y Barco cargados (para generar PDF individual).</summary>
    Task<Voucher?> GetByIdConDetalleAsync(int id, CancellationToken ct = default);
}
