using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Enums;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Core.Models.Resultados;
using PuertoBB.Infrastructure.Data;

namespace PuertoBB.Infrastructure.Repositories.CentroMaritimo;

public class ReciboRepository : RepositoryBase<Recibo>, IReciboRepository
{
    private readonly CentroMaritimoDbContext _db;

    public ReciboRepository(CentroMaritimoDbContext db, ILogger<ReciboRepository> logger) : base(db, logger)
        => _db = db;

    public Task<Recibo?> GetConDetalleAsync(int id, CancellationToken ct = default)
        => _db.Recibos
            .Include(r => r.Cliente).ThenInclude(a => a.Emails)
            .Include(r => r.NotaDeCredito)
            .Include(r => r.Lineas)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> ExisteAsync(int agenciaId, int? grupoId, int anio, int mes, CancellationToken ct = default)
        => FiltrarPorClave(_db.Recibos, agenciaId, grupoId, anio, mes).AnyAsync(ct);

    public Task<bool> ExisteComprobanteAsync(int puntoVenta, int codigoAfip, long numero, CancellationToken ct = default)
        => _db.Recibos.AnyAsync(r => r.PuntoDeVenta == puntoVenta && r.CodigoAfip == codigoAfip && r.NumeroComprobante == numero, ct);

    public Task<Recibo?> GetPorClaveAsync(int agenciaId, int? grupoId, int anio, int mes, CancellationToken ct = default)
        => FiltrarPorClave(_db.Recibos
                .Include(r => r.Cliente).ThenInclude(a => a.Emails)
                .Include(r => r.Lineas), agenciaId, grupoId, anio, mes)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Clave de emisión de cuota: agencia + grupo (vía EmisionGrupo; null = individual) + período.
    /// Para individuales (grupoId null) excluye consolidados (recibos referenciados por una Consolidacion)
    /// y solo retoma el recibo Pendiente.
    /// </summary>
    private IQueryable<Recibo> FiltrarPorClave(IQueryable<Recibo> q, int agenciaId, int? grupoId, int anio, int mes)
    {
        q = q.Where(r => r.ClienteId == agenciaId && r.PeriodoAnio == anio && r.PeriodoMes == mes);
        return grupoId is int gid
            ? q.Where(r => r.EmisionGrupo != null && r.EmisionGrupo.GrupoFacturacionId == gid)
            : q.Where(r => r.EmisionGrupo == null
                           && !_db.Consolidaciones.Any(c => c.ReciboId == r.Id)
                           && r.EstadoFiscal == EstadoFiscal.Pendiente);
    }

    public async Task<IReadOnlyList<Recibo>> GetPorGrupoYPeriodoAsync(int grupoId, int anio, int mes, CancellationToken ct = default)
        => await _db.Recibos
            .Include(r => r.Cliente).ThenInclude(a => a.Emails)
            .Include(r => r.Lineas)
            .Where(r => r.EmisionGrupo != null && r.EmisionGrupo.GrupoFacturacionId == grupoId &&
                        r.PeriodoAnio == anio && r.PeriodoMes == mes)
            .ToListAsync(ct);

    public async Task EliminarPendienteAsync(int reciboId, CancellationToken ct = default)
    {
        var recibo = await _db.Recibos
            .Include(r => r.Lineas)
            .Include(r => r.EmisionGrupo)
            .FirstOrDefaultAsync(r => r.Id == reciboId, ct);
        if (recibo is null) return;

        // Si es un consolidado, liberar sus vouchers (vuelven a "libres") y borrar la consolidación.
        var consolidacion = await _db.Consolidaciones
            .Include(c => c.Vouchers)
            .FirstOrDefaultAsync(c => c.ReciboId == reciboId, ct);
        if (consolidacion is not null)
        {
            foreach (var v in consolidacion.Vouchers) v.ConsolidacionId = null;
            _db.Consolidaciones.Remove(consolidacion);
        }

        if (recibo.EmisionGrupo is not null) _db.Remove(recibo.EmisionGrupo);
        _db.RemoveRange(recibo.Lineas);
        _db.Recibos.Remove(recibo);
        await GuardarAsync(ct);
    }

    // Un recibo por voucher (Individual) no cuenta como "consolidado" de la agencia.
    public Task<bool> ExisteConsolidadoAsync(int agenciaId, int anio, int mes, CancellationToken ct = default)
        => _db.Consolidaciones.AnyAsync(c =>
            !c.Individual &&
            c.ClienteId == agenciaId &&
            c.PeriodoAnio == anio &&
            c.PeriodoMes == mes &&
            c.Recibo.EstadoFiscal != EstadoFiscal.Anulado, ct);

    // El reintento del consolidado ignora los recibos por voucher (Individual): no son work-in-progress del consolidado.
    public Task<Consolidacion?> GetConsolidacionPendienteAsync(int agenciaId, int anio, int mes, CancellationToken ct = default)
        => _db.Consolidaciones
            .Include(c => c.Recibo).ThenInclude(r => r.Cliente).ThenInclude(a => a.Emails)
            .Include(c => c.Recibo).ThenInclude(r => r.Lineas)
            .Include(c => c.Vouchers).ThenInclude(v => v.Barco)
            .FirstOrDefaultAsync(c => c.ClienteId == agenciaId && c.PeriodoAnio == anio &&
                                      c.PeriodoMes == mes && c.Pendiente && !c.Individual, ct);

    public Task<Consolidacion?> GetConsolidacionByReciboAsync(int reciboId, CancellationToken ct = default)
        => _db.Consolidaciones
            .Include(c => c.Vouchers).ThenInclude(v => v.Barco)
            .FirstOrDefaultAsync(c => c.ReciboId == reciboId, ct);

    public Task<IReadOnlyList<int>> GetClientesConConsolidacionPendienteAsync(int anio, int mes, CancellationToken ct = default)
        => _db.Consolidaciones
            .Where(c => c.Pendiente && !c.Individual && c.PeriodoAnio == anio && c.PeriodoMes == mes)
            .Select(c => c.ClienteId)
            .Distinct()
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<int>)t.Result, TaskContinuationOptions.ExecuteSynchronously);

    public async Task AddConsolidacionConVouchersAsync(Consolidacion consolidacion, IReadOnlyList<int> voucherIds, CancellationToken ct = default)
    {
        var now = DateTime.Now;
        consolidacion.CreatedAt = now;
        consolidacion.Recibo.CreatedAt = now;
        // Add de la consolidación inserta también su Recibo (grafo de entidades nuevas).
        _db.Consolidaciones.Add(consolidacion);
        var vouchers = await _db.Vouchers.Where(v => voucherIds.Contains(v.Id)).ToListAsync(ct);
        foreach (var v in vouchers) v.Consolidacion = consolidacion;
        await GuardarAsync(ct);
    }

    public async Task AnularConNotaAsync(Recibo recibo, NotaDeCredito nota, CancellationToken ct = default)
    {
        recibo.EstadoFiscal = EstadoFiscal.Anulado;
        recibo.UpdatedAt = DateTime.Now;
        nota.CreatedAt = DateTime.Now;

        // Desvincular vouchers del consolidado para permitir reemisión del período (P1-3).
        var consolidacion = await _db.Consolidaciones
            .Include(c => c.Vouchers)
            .FirstOrDefaultAsync(c => c.ReciboId == recibo.Id, ct);
        if (consolidacion is not null)
        {
            foreach (var v in consolidacion.Vouchers) v.ConsolidacionId = null;
            consolidacion.Pendiente = false; // ya no es un work-in-progress
        }

        _db.Set<NotaDeCredito>().Add(nota);
        await GuardarAsync(ct);
    }

    public async Task<IReadOnlyList<Recibo>> GetPendientesAsync(FiltroPendientes f, CancellationToken ct = default)
    {
        var q = _db.Recibos.AsNoTracking().Include(r => r.Cliente).AsQueryable();

        if (f.PeriodoAnio is int anio)        q = q.Where(r => r.PeriodoAnio == anio);
        if (f.PeriodoMes is int mes)          q = q.Where(r => r.PeriodoMes == mes);
        if (f.GrupoFacturacionId is int gid)  q = q.Where(r => r.EmisionGrupo != null && r.EmisionGrupo.GrupoFacturacionId == gid);
        if (f.ClienteId is int eid)           q = q.Where(r => r.ClienteId == eid);
        if (f.Estado is { } estado)           q = q.Where(r => r.EstadoFiscal == estado);
        if (f.ExcluirIncobrables)              q = q.Where(r => r.FechaIncobrable == null);

        var hoy = DateTime.Today;
        if (f.SoloVencidos)
            q = q.Where(r => r.FechaVencimientoPago < hoy &&
                             r.EstadoFiscal == EstadoFiscal.Emitido &&
                             r.FechaPago == null && r.FechaIncobrable == null);

        return await q.OrderByDescending(r => r.PeriodoAnio).ThenByDescending(r => r.PeriodoMes).ThenBy(r => r.Cliente.Nombre).ToListAsync(ct);
    }

    public async Task<PaginaResultado<Recibo>> GetControlPaginadoAsync(FiltroControlPagos f, CancellationToken ct = default)
    {
        var hoy = DateTime.Today;
        var q = AplicarEstado(_db.Recibos.AsNoTracking().Include(r => r.Cliente), f.Estado, hoy);

        var total = await q.CountAsync(ct);
        var vencidos = await q.CountAsync(r =>
            r.EstadoFiscal == EstadoFiscal.Emitido && r.FechaPago == null && r.FechaIncobrable == null
            && r.FechaVencimientoPago < hoy, ct);

        var tamanio = f.TamanioPagina > 0 ? f.TamanioPagina : 100;
        var totalPaginas = Math.Max(1, (int)Math.Ceiling((double)total / tamanio));
        var pagina = Math.Clamp(f.Pagina, 1, totalPaginas);

        var items = await Ordenar(q)
            .Skip((pagina - 1) * tamanio).Take(tamanio)
            .ToListAsync(ct);

        return new PaginaResultado<Recibo>(items, total, vencidos, pagina, tamanio);
    }

    public async Task<IReadOnlyList<Recibo>> GetControlCandidatosAsync(FiltroControlPagos f, CancellationToken ct = default)
        => await Ordenar(AplicarEstado(_db.Recibos.AsNoTracking().Include(r => r.Cliente), f.Estado, DateTime.Today))
            .ToListAsync(ct);

    /// <summary>Predicado de estado de la sección "Control" (espejo de EstadoReciboHelper.EtiquetaEstado).
    /// "Todos" excluye los Pendientes sin CAE: Control solo muestra comprobantes con CAE (Emitido o Anulado).</summary>
    private static IQueryable<Recibo> AplicarEstado(IQueryable<Recibo> q, FiltroEstadoControl estado, DateTime hoy)
        => estado switch
        {
            FiltroEstadoControl.PendientesDePago => q.Where(r =>
                r.EstadoFiscal == EstadoFiscal.Emitido && r.FechaPago == null && r.FechaIncobrable == null),
            FiltroEstadoControl.Vencido => q.Where(r =>
                r.EstadoFiscal == EstadoFiscal.Emitido && r.FechaPago == null && r.FechaIncobrable == null
                && r.FechaVencimientoPago < hoy),
            FiltroEstadoControl.Pagado => q.Where(r =>
                r.EstadoFiscal == EstadoFiscal.Emitido && r.FechaIncobrable == null && r.FechaPago != null),
            FiltroEstadoControl.Incobrable => q.Where(r =>
                r.EstadoFiscal == EstadoFiscal.Emitido && r.FechaIncobrable != null),
            FiltroEstadoControl.Anulado => q.Where(r => r.EstadoFiscal == EstadoFiscal.Anulado),
            _ => q.Where(r => r.EstadoFiscal != EstadoFiscal.Pendiente), // Todos (solo con CAE)
        };

    private static IOrderedQueryable<Recibo> Ordenar(IQueryable<Recibo> q)
        => q.OrderByDescending(r => r.PeriodoAnio).ThenByDescending(r => r.PeriodoMes).ThenBy(r => r.Cliente.Nombre);

    public async Task<IReadOnlyList<Recibo>> GetPorPeriodoAsync(int anio, int mes, CancellationToken ct = default)
        => await _db.Recibos.AsNoTracking().Include(r => r.Cliente)
            .Include(r => r.NotaDeCredito)
            .Where(r => r.PeriodoAnio == anio && r.PeriodoMes == mes)
            .OrderBy(r => r.Cliente.Nombre).ToListAsync(ct);
}
