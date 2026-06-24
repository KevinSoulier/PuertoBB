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

    /// <summary>Recibo de cuota rastreado (con Cliente+Emails y Líneas) para (agencia, grupo, período), o null. Para crear-o-resumir.</summary>
    Task<Recibo?> GetPorClaveAsync(int agenciaId, int? grupoId, int anio, int mes, CancellationToken ct = default);

    /// <summary>True si ya existe un consolidado de vouchers (no anulado) para (agencia, período).</summary>
    Task<bool> ExisteConsolidadoAsync(int agenciaId, int anio, int mes, CancellationToken ct = default);

    /// <summary>Consolidación <b>Pendiente</b> (recibo sin CAE) del período, con su Recibo (Cliente.Emails + Lineas)
    /// y Vouchers (con Barco), o null. Es el target de reintento: a lo sumo una por (agencia, período) por el índice
    /// único parcial. Los consolidados ya emitidos (con CAE) no se devuelven; un voucher nuevo genera un complementario.</summary>
    Task<Consolidacion?> GetConsolidacionPendienteAsync(int agenciaId, int anio, int mes, CancellationToken ct = default);

    /// <summary>La consolidación de un recibo (con Vouchers + Barco), o null si el recibo no es un consolidado.</summary>
    Task<Consolidacion?> GetConsolidacionByReciboAsync(int reciboId, CancellationToken ct = default);

    /// <summary>Persiste la consolidación + su recibo y vincula los vouchers en UN solo SaveChanges (atómico).</summary>
    Task AddConsolidacionConVouchersAsync(Consolidacion consolidacion, IReadOnlyList<int> voucherIds, CancellationToken ct = default);

    /// <summary>IDs de agencias que tienen una consolidación Pendiente (recibo sin CAE) para el período, para reintentar.</summary>
    Task<IReadOnlyList<int>> GetClientesConConsolidacionPendienteAsync(int anio, int mes, CancellationToken ct = default);

    /// <summary>Marca el recibo Anulado, libera los vouchers de su consolidación y persiste la NC en UN solo SaveChanges.</summary>
    Task AnularConNotaAsync(Recibo recibo, NotaDeCredito nota, CancellationToken ct = default);

    /// <summary>Elimina un recibo (Líneas + vínculo EmisionGrupo, y libera/borra su consolidación) en UN solo SaveChanges.
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
