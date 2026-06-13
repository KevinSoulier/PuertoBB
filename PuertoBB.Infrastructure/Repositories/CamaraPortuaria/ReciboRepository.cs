using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;
using PuertoBB.Core.Models.Resultados;
using PuertoBB.Infrastructure.Data;

namespace PuertoBB.Infrastructure.Repositories.CamaraPortuaria;

public class ReciboRepository : RepositoryBase<Recibo>, IReciboRepository
{
    private readonly CamaraPortuariaDbContext _db;

    public ReciboRepository(CamaraPortuariaDbContext db, ILogger<ReciboRepository> logger) : base(db, logger)
        => _db = db;

    public Task<Recibo?> GetConDetalleAsync(int id, CancellationToken ct = default)
        => _db.Recibos
            .Include(r => r.Empresa).ThenInclude(e => e.Emails)
            .Include(r => r.NotaDeCredito)
            .Include(r => r.Lineas)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> ExisteAsync(int empresaId, int? grupoId, int anio, int mes, CancellationToken ct = default)
        => FiltrarPorClave(_db.Recibos, empresaId, grupoId, anio, mes).AnyAsync(ct);

    public Task<bool> ExisteComprobanteAsync(int puntoVenta, int codigoAfip, long numero, CancellationToken ct = default)
        => _db.Recibos.AnyAsync(r => r.PuntoDeVenta == puntoVenta && r.CodigoAfip == codigoAfip && r.NumeroComprobante == numero, ct);

    public Task<Recibo?> GetPorClaveAsync(int empresaId, int? grupoId, int anio, int mes, CancellationToken ct = default)
        => FiltrarPorClave(_db.Recibos
                .Include(r => r.Empresa).ThenInclude(e => e.Emails)
                .Include(r => r.Lineas), empresaId, grupoId, anio, mes)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Clave de emisión: empresa + grupo (vía EmisionGrupo; null = individual) + período.
    /// Para individuales (grupoId null) solo retorna el recibo Pendiente (sin CAE) — permite N recibos individuales
    /// por período; el reintento retoma el Pendiente; completados quedan independientes.
    /// </summary>
    private static IQueryable<Recibo> FiltrarPorClave(IQueryable<Recibo> q, int empresaId, int? grupoId, int anio, int mes)
    {
        q = q.Where(r => r.EmpresaId == empresaId && r.PeriodoAnio == anio && r.PeriodoMes == mes);
        return grupoId is int gid
            ? q.Where(r => r.EmisionGrupo != null && r.EmisionGrupo.GrupoFacturacionId == gid)
            : q.Where(r => r.EmisionGrupo == null && r.Estado == Core.Enums.ReciboEstado.Pendiente);
    }

    public async Task AnularConNotaAsync(Recibo recibo, Core.Entities.CamaraPortuaria.NotaDeCredito nota, CancellationToken ct = default)
    {
        recibo.Estado = Core.Enums.ReciboEstado.Anulado;
        recibo.UpdatedAt = DateTime.Now;
        nota.CreatedAt = DateTime.Now;
        _db.Set<Core.Entities.CamaraPortuaria.NotaDeCredito>().Add(nota);
        await GuardarAsync(ct);
    }

    public async Task<IReadOnlyList<Recibo>> GetPorGrupoYPeriodoAsync(int grupoId, int anio, int mes, CancellationToken ct = default)
        => await _db.Recibos
            .Include(r => r.Empresa).ThenInclude(e => e.Emails)
            .Include(r => r.Lineas)
            .Where(r => r.EmisionGrupo != null && r.EmisionGrupo.GrupoFacturacionId == grupoId &&
                        r.PeriodoAnio == anio && r.PeriodoMes == mes)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Recibo>> GetPendientesAsync(FiltroPendientes f, CancellationToken ct = default)
    {
        var q = _db.Recibos.AsNoTracking().Include(r => r.Empresa).AsQueryable();

        if (f.PeriodoAnio is int anio)        q = q.Where(r => r.PeriodoAnio == anio);
        if (f.PeriodoMes is int mes)          q = q.Where(r => r.PeriodoMes == mes);
        if (f.GrupoFacturacionId is int gid)  q = q.Where(r => r.EmisionGrupo != null && r.EmisionGrupo.GrupoFacturacionId == gid);
        if (f.EntidadId is int eid)           q = q.Where(r => r.EmpresaId == eid);
        if (f.Estado is { } estado)           q = q.Where(r => r.Estado == estado);
        if (f.ExcluirMorosos)                  q = q.Where(r => !r.Empresa.EsMoroso);

        var hoy = DateTime.Today;
        if (f.SoloVencidos)
            q = q.Where(r => r.FechaVencimientoPago < hoy &&
                             (r.Estado == Core.Enums.ReciboEstado.Emitido || r.Estado == Core.Enums.ReciboEstado.Enviado));

        return await q.OrderByDescending(r => r.PeriodoAnio).ThenByDescending(r => r.PeriodoMes).ThenBy(r => r.Empresa.Nombre).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Recibo>> GetPorPeriodoAsync(int anio, int mes, CancellationToken ct = default)
        => await _db.Recibos.AsNoTracking().Include(r => r.Empresa)
            .Include(r => r.NotaDeCredito)
            .Where(r => r.PeriodoAnio == anio && r.PeriodoMes == mes)
            .OrderBy(r => r.Empresa.Nombre).ToListAsync(ct);
}
