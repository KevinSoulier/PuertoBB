using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Models.Resultados;

namespace PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;

public interface IReciboRepository : IRepository<Recibo>
{
    Task<Recibo?> GetConDetalleAsync(int id, CancellationToken ct = default);

    /// <summary>True si ya existe un recibo para (agencia, grupo, período).</summary>
    Task<bool> ExisteAsync(int agenciaId, int? grupoId, int anio, int mes, CancellationToken ct = default);

    /// <summary>True si ya existe un recibo consolidado de vouchers para (agencia, período).</summary>
    Task<bool> ExisteConsolidadoAsync(int agenciaId, int anio, int mes, CancellationToken ct = default);

    Task<IReadOnlyList<Recibo>> GetPendientesAsync(FiltroPendientes filtro, CancellationToken ct = default);
    Task<IReadOnlyList<Recibo>> GetPorPeriodoAsync(int anio, int mes, CancellationToken ct = default);
}
