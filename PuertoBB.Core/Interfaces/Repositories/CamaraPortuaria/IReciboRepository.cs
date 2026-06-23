using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Models.Resultados;

namespace PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;

public interface IReciboRepository : IRepository<Recibo>
{
    /// <summary>Recibo con Empresa y Grupo cargados.</summary>
    Task<Recibo?> GetConDetalleAsync(int id, CancellationToken ct = default);

    /// <summary>True si ya existe un recibo para (empresa, grupo, período).</summary>
    Task<bool> ExisteAsync(int empresaId, int? grupoId, int anio, int mes, CancellationToken ct = default);

    /// <summary>True si ya hay un recibo local con ese (punto de venta, código AFIP, número). Para no adoptar dos veces un mismo comprobante al recuperar.</summary>
    Task<bool> ExisteComprobanteAsync(int puntoVenta, int codigoAfip, long numero, CancellationToken ct = default);

    /// <summary>Recibo rastreado (con Empresa+Emails) para (empresa, grupo, período), o null. Para crear-o-resumir.</summary>
    Task<Recibo?> GetPorClaveAsync(int empresaId, int? grupoId, int anio, int mes, CancellationToken ct = default);

    /// <summary>Marca el recibo Anulado y persiste la NC en UN solo SaveChanges.</summary>
    Task AnularConNotaAsync(Recibo recibo, Core.Entities.CamaraPortuaria.NotaDeCredito nota, CancellationToken ct = default);

    /// <summary>Elimina un recibo (junto con sus Líneas y el vínculo EmisionGrupo) en UN solo SaveChanges.
    /// Usado para recuperar un recibo Pendiente (sin CAE) trabado por una emisión fallida.</summary>
    Task EliminarPendienteAsync(int reciboId, CancellationToken ct = default);

    /// <summary>Recibos que matchean el filtro del dashboard (con Empresa/Grupo cargados).</summary>
    Task<IReadOnlyList<Recibo>> GetPendientesAsync(FiltroPendientes filtro, CancellationToken ct = default);

    /// <summary>Una página de recibos para la sección "Control": filtra por estado en la base
    /// (paginado server-side) y devuelve la página + total + contador de vencidos. La búsqueda por
    /// texto la resuelve el servicio en memoria sobre <see cref="GetControlCandidatosAsync"/>.</summary>
    Task<PaginaResultado<Recibo>> GetControlPaginadoAsync(FiltroControlPagos filtro, CancellationToken ct = default);

    /// <summary>Todos los recibos que matchean el estado del filtro (ordenados como el paginado,
    /// con Empresa cargada), sin filtro de texto ni paginado. Base para la búsqueda en memoria.</summary>
    Task<IReadOnlyList<Recibo>> GetControlCandidatosAsync(FiltroControlPagos filtro, CancellationToken ct = default);

    /// <summary>Todos los recibos de un período (para la grilla de recibos).</summary>
    Task<IReadOnlyList<Recibo>> GetPorPeriodoAsync(int anio, int mes, CancellationToken ct = default);

    /// <summary>Recibos de un grupo en un período (para la tabla de emisión masiva).</summary>
    Task<IReadOnlyList<Recibo>> GetPorGrupoYPeriodoAsync(int grupoId, int anio, int mes, CancellationToken ct = default);
}
