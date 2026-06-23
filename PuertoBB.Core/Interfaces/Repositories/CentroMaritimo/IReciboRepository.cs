using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Models.Resultados;

namespace PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;

public interface IReciboRepository : IRepository<Recibo>
{
    Task<Recibo?> GetConDetalleAsync(int id, CancellationToken ct = default);

    /// <summary>True si ya existe un recibo para (agencia, grupo, período).</summary>
    Task<bool> ExisteAsync(int agenciaId, int? grupoId, int anio, int mes, CancellationToken ct = default);

    /// <summary>True si ya hay un recibo local con ese (punto de venta, código AFIP, número). Para no adoptar dos veces un mismo comprobante al recuperar.</summary>
    Task<bool> ExisteComprobanteAsync(int puntoVenta, int codigoAfip, long numero, CancellationToken ct = default);

    /// <summary>Recibo rastreado (con Cliente+Emails y Vouchers) para (agencia, grupo, período), o null. Para crear-o-resumir.</summary>
    Task<Recibo?> GetPorClaveAsync(int agenciaId, int? grupoId, int anio, int mes, CancellationToken ct = default);

    /// <summary>True si ya existe un recibo consolidado de vouchers para (agencia, período).</summary>
    Task<bool> ExisteConsolidadoAsync(int agenciaId, int anio, int mes, CancellationToken ct = default);

    /// <summary>Recibo consolidado <b>Pendiente</b> (sin CAE) del período, con Vouchers/Lineas/Cliente.Emails,
    /// o null. Es el target de reintento: a lo sumo uno por (agencia, período) por el índice único.
    /// Los consolidados ya emitidos (con CAE) no se devuelven; un voucher nuevo genera un complementario.</summary>
    Task<Recibo?> GetConsolidadoPendienteAsync(int agenciaId, int anio, int mes, CancellationToken ct = default);

    /// <summary>Persiste el recibo y vincula los vouchers en UN solo SaveChanges (atómico).</summary>
    Task AddConVouchersAsync(Recibo recibo, IReadOnlyList<int> voucherIds, CancellationToken ct = default);

    /// <summary>IDs de agencias que tienen un recibo consolidado en estado Pendiente (sin CAE) para el período, para reintentar.</summary>
    Task<IReadOnlyList<int>> GetClientesConConsolidadoPendienteAsync(int anio, int mes, CancellationToken ct = default);

    /// <summary>Marca el recibo Anulado, desvincula vouchers de consolidados y persiste la NC en UN solo SaveChanges.</summary>
    Task AnularConNotaAsync(Recibo recibo, Core.Entities.CentroMaritimo.NotaDeCredito nota, CancellationToken ct = default);

    /// <summary>Elimina un recibo (Líneas + vínculo EmisionGrupo, y libera los vouchers consolidados) en UN solo SaveChanges.
    /// Usado para recuperar un recibo Pendiente (sin CAE) trabado por una emisión fallida.</summary>
    Task EliminarPendienteAsync(int reciboId, CancellationToken ct = default);

    Task<IReadOnlyList<Recibo>> GetPendientesAsync(FiltroPendientes filtro, CancellationToken ct = default);

    /// <summary>Una página de recibos para la sección "Control": filtra por estado en la base
    /// (paginado server-side) y devuelve la página + total + contador de vencidos. La búsqueda por
    /// texto la resuelve el servicio en memoria sobre <see cref="GetControlCandidatosAsync"/>.</summary>
    Task<PaginaResultado<Recibo>> GetControlPaginadoAsync(FiltroControlPagos filtro, CancellationToken ct = default);

    /// <summary>Todos los recibos que matchean el estado del filtro (ordenados como el paginado,
    /// con Cliente cargada), sin filtro de texto ni paginado. Base para la búsqueda en memoria.</summary>
    Task<IReadOnlyList<Recibo>> GetControlCandidatosAsync(FiltroControlPagos filtro, CancellationToken ct = default);

    Task<IReadOnlyList<Recibo>> GetPorPeriodoAsync(int anio, int mes, CancellationToken ct = default);

    /// <summary>Recibos de un grupo en un período (para la tabla de emisión masiva).</summary>
    Task<IReadOnlyList<Recibo>> GetPorGrupoYPeriodoAsync(int grupoId, int anio, int mes, CancellationToken ct = default);
}
