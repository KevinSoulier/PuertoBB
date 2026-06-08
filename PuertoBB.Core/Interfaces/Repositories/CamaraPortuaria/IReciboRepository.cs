using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Models.Resultados;

namespace PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;

public interface IReciboRepository : IRepository<Recibo>
{
    /// <summary>Recibo con Empresa y Grupo cargados.</summary>
    Task<Recibo?> GetConDetalleAsync(int id, CancellationToken ct = default);

    /// <summary>True si ya existe un recibo para (empresa, grupo, período).</summary>
    Task<bool> ExisteAsync(int empresaId, int? grupoId, int anio, int mes, CancellationToken ct = default);

    /// <summary>Recibo rastreado (con Empresa+Emails) para (empresa, grupo, período), o null. Para crear-o-resumir.</summary>
    Task<Recibo?> GetPorClaveAsync(int empresaId, int? grupoId, int anio, int mes, CancellationToken ct = default);

    /// <summary>Recibos que matchean el filtro del dashboard (con Empresa/Grupo cargados).</summary>
    Task<IReadOnlyList<Recibo>> GetPendientesAsync(FiltroPendientes filtro, CancellationToken ct = default);

    /// <summary>Todos los recibos de un período (para la grilla de recibos).</summary>
    Task<IReadOnlyList<Recibo>> GetPorPeriodoAsync(int anio, int mes, CancellationToken ct = default);
}
