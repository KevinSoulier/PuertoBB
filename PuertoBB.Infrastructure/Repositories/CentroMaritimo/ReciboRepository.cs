using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Entities.CentroMaritimo;
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
            .Include(r => r.Agencia).ThenInclude(a => a.Emails)
            .Include(r => r.Vouchers).ThenInclude(v => v.Barco)
            .Include(r => r.NotaDeCredito)
            .Include(r => r.Lineas)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> ExisteAsync(int agenciaId, int? grupoId, int anio, int mes, CancellationToken ct = default)
        => FiltrarPorClave(_db.Recibos, agenciaId, grupoId, anio, mes).AnyAsync(ct);

    public Task<bool> ExisteComprobanteAsync(int puntoVenta, int codigoAfip, long numero, CancellationToken ct = default)
        => _db.Recibos.AnyAsync(r => r.PuntoDeVenta == puntoVenta && r.CodigoAfip == codigoAfip && r.NumeroComprobante == numero, ct);

    public Task<Recibo?> GetPorClaveAsync(int agenciaId, int? grupoId, int anio, int mes, CancellationToken ct = default)
        => FiltrarPorClave(_db.Recibos
                .Include(r => r.Agencia).ThenInclude(a => a.Emails)
                .Include(r => r.Vouchers).ThenInclude(v => v.Barco)
                .Include(r => r.Lineas), agenciaId, grupoId, anio, mes)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Clave de emisión: agencia + grupo (vía EmisionGrupo; null = individual) + período.
    /// Para individuales (grupoId null) excluye consolidados y solo retoma el recibo Pendiente.
    /// </summary>
    private static IQueryable<Recibo> FiltrarPorClave(IQueryable<Recibo> q, int agenciaId, int? grupoId, int anio, int mes)
    {
        q = q.Where(r => r.AgenciaId == agenciaId && r.PeriodoAnio == anio && r.PeriodoMes == mes);
        return grupoId is int gid
            ? q.Where(r => r.EmisionGrupo != null && r.EmisionGrupo.GrupoFacturacionId == gid)
            : q.Where(r => r.EmisionGrupo == null && !r.EsConsolidadoVouchers && r.Estado == Core.Enums.ReciboEstado.Pendiente);
    }

    public async Task<IReadOnlyList<Recibo>> GetPorGrupoYPeriodoAsync(int grupoId, int anio, int mes, CancellationToken ct = default)
        => await _db.Recibos
            .Include(r => r.Agencia).ThenInclude(a => a.Emails)
            .Include(r => r.Lineas)
            .Where(r => r.EmisionGrupo != null && r.EmisionGrupo.GrupoFacturacionId == grupoId &&
                        r.PeriodoAnio == anio && r.PeriodoMes == mes)
            .ToListAsync(ct);

    public Task<bool> ExisteConsolidadoAsync(int agenciaId, int anio, int mes, CancellationToken ct = default)
        => _db.Recibos.AnyAsync(r =>
            r.AgenciaId == agenciaId &&
            r.EsConsolidadoVouchers &&
            r.PeriodoAnio == anio &&
            r.PeriodoMes == mes &&
            r.Estado != Core.Enums.ReciboEstado.Anulado, ct);

    public Task<Recibo?> GetConsolidadoAsync(int agenciaId, int anio, int mes, CancellationToken ct = default)
        => _db.Recibos
            .Include(r => r.Agencia).ThenInclude(a => a.Emails)
            .Include(r => r.Vouchers).ThenInclude(v => v.Barco)
            .Include(r => r.Lineas)
            .FirstOrDefaultAsync(r => r.AgenciaId == agenciaId && r.EsConsolidadoVouchers &&
                                      r.PeriodoAnio == anio && r.PeriodoMes == mes &&
                                      r.Estado != Core.Enums.ReciboEstado.Anulado, ct);

    public Task<IReadOnlyList<int>> GetAgenciasConConsolidadoPendienteAsync(int anio, int mes, CancellationToken ct = default)
        => _db.Recibos
            .Where(r => r.EsConsolidadoVouchers && r.Estado == Core.Enums.ReciboEstado.Pendiente &&
                        r.PeriodoAnio == anio && r.PeriodoMes == mes)
            .Select(r => r.AgenciaId)
            .Distinct()
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<int>)t.Result, TaskContinuationOptions.ExecuteSynchronously);

    public async Task AddConVouchersAsync(Recibo recibo, IReadOnlyList<int> voucherIds, CancellationToken ct = default)
    {
        recibo.CreatedAt = DateTime.Now;
        _db.Recibos.Add(recibo);
        var vouchers = await _db.Vouchers.Where(v => voucherIds.Contains(v.Id)).ToListAsync(ct);
        foreach (var v in vouchers) v.Recibo = recibo;
        await GuardarAsync(ct);
    }

    public async Task AnularConNotaAsync(Recibo recibo, Core.Entities.CentroMaritimo.NotaDeCredito nota, CancellationToken ct = default)
    {
        recibo.Estado = Core.Enums.ReciboEstado.Anulado;
        recibo.UpdatedAt = DateTime.Now;
        nota.CreatedAt = DateTime.Now;
        // Desvincular vouchers del consolidado para permitir reemisión del período (P1-3).
        if (recibo.EsConsolidadoVouchers)
            foreach (var v in recibo.Vouchers) v.ReciboId = null;
        _db.Set<Core.Entities.CentroMaritimo.NotaDeCredito>().Add(nota);
        await GuardarAsync(ct);
    }

    public async Task<IReadOnlyList<Recibo>> GetPendientesAsync(FiltroPendientes f, CancellationToken ct = default)
    {
        var q = _db.Recibos.AsNoTracking().Include(r => r.Agencia).AsQueryable();

        if (f.PeriodoAnio is int anio)        q = q.Where(r => r.PeriodoAnio == anio);
        if (f.PeriodoMes is int mes)          q = q.Where(r => r.PeriodoMes == mes);
        if (f.GrupoFacturacionId is int gid)  q = q.Where(r => r.EmisionGrupo != null && r.EmisionGrupo.GrupoFacturacionId == gid);
        if (f.EntidadId is int eid)           q = q.Where(r => r.AgenciaId == eid);
        if (f.Estado is { } estado)           q = q.Where(r => r.Estado == estado);
        if (f.ExcluirMorosos)                  q = q.Where(r => !r.Agencia.EsMoroso);

        var hoy = DateTime.Today;
        if (f.SoloVencidos)
            q = q.Where(r => r.FechaVencimientoPago < hoy &&
                             (r.Estado == Core.Enums.ReciboEstado.Emitido || r.Estado == Core.Enums.ReciboEstado.Enviado));

        return await q.OrderByDescending(r => r.PeriodoAnio).ThenByDescending(r => r.PeriodoMes).ThenBy(r => r.Agencia.Nombre).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Recibo>> GetPorPeriodoAsync(int anio, int mes, CancellationToken ct = default)
        => await _db.Recibos.AsNoTracking().Include(r => r.Agencia)
            .Include(r => r.NotaDeCredito)
            .Where(r => r.PeriodoAnio == anio && r.PeriodoMes == mes)
            .OrderBy(r => r.Agencia.Nombre).ToListAsync(ct);
}
