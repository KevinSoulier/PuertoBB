using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;

public interface IVoucherRepository : IRepository<Voucher>
{
    /// <summary>Vouchers sin consolidar en el período (ReciboId IS NULL), con Agencia y Barco.</summary>
    Task<IReadOnlyList<Voucher>> GetPendientesByPeriodoAsync(int anio, int mes, CancellationToken ct = default);

    /// <summary>Agencias distintas que tienen vouchers pendientes en el período.</summary>
    Task<IReadOnlyList<Agencia>> GetAgenciasConVouchersPendientesAsync(int anio, int mes, CancellationToken ct = default);

    /// <summary>Asigna ReciboId a un conjunto de vouchers (consolidación).</summary>
    Task MarcarConsolidadosAsync(IEnumerable<int> voucherIds, int reciboId, CancellationToken ct = default);

    /// <summary>Vouchers de una agencia (con Barco), opcionalmente filtrados por período.</summary>
    Task<IReadOnlyList<Voucher>> GetPorAgenciaAsync(int agenciaId, int? anio = null, int? mes = null, CancellationToken ct = default);
}
