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
            .Include(r => r.Grupo)
            .Include(r => r.NotaDeCredito)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> ExisteAsync(int empresaId, int? grupoId, int anio, int mes, CancellationToken ct = default)
        => _db.Recibos.AnyAsync(r =>
            r.EmpresaId == empresaId &&
            r.GrupoFacturacionId == grupoId &&
            r.PeriodoAnio == anio &&
            r.PeriodoMes == mes, ct);

    public Task<Recibo?> GetPorClaveAsync(int empresaId, int? grupoId, int anio, int mes, CancellationToken ct = default)
        => _db.Recibos
            .Include(r => r.Empresa).ThenInclude(e => e.Emails)
            .FirstOrDefaultAsync(r =>
                r.EmpresaId == empresaId &&
                r.GrupoFacturacionId == grupoId &&
                r.PeriodoAnio == anio &&
                r.PeriodoMes == mes, ct);

    public async Task<IReadOnlyList<Recibo>> GetPendientesAsync(FiltroPendientes f, CancellationToken ct = default)
    {
        var q = _db.Recibos.AsNoTracking().Include(r => r.Empresa).Include(r => r.Grupo).AsQueryable();

        if (f.PeriodoAnio is int anio)        q = q.Where(r => r.PeriodoAnio == anio);
        if (f.PeriodoMes is int mes)          q = q.Where(r => r.PeriodoMes == mes);
        if (f.GrupoFacturacionId is int gid)  q = q.Where(r => r.GrupoFacturacionId == gid);
        if (f.EntidadId is int eid)           q = q.Where(r => r.EmpresaId == eid);
        if (f.Estado is { } estado)           q = q.Where(r => r.Estado == estado);

        var hoy = DateTime.Today;
        if (f.SoloVencidos)
            q = q.Where(r => r.FechaVencimientoPago < hoy &&
                             (r.Estado == Core.Enums.ReciboEstado.Emitido || r.Estado == Core.Enums.ReciboEstado.Enviado));

        return await q.OrderByDescending(r => r.PeriodoAnio).ThenByDescending(r => r.PeriodoMes).ThenBy(r => r.Empresa.Nombre).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Recibo>> GetPorPeriodoAsync(int anio, int mes, CancellationToken ct = default)
        => await _db.Recibos.AsNoTracking().Include(r => r.Empresa).Include(r => r.Grupo)
            .Where(r => r.PeriodoAnio == anio && r.PeriodoMes == mes)
            .OrderBy(r => r.Empresa.Nombre).ToListAsync(ct);
}
