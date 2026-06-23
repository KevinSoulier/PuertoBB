using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Infrastructure.Data;

namespace PuertoBB.Infrastructure.Repositories.CentroMaritimo;

public class VoucherRepository : RepositoryBase<Voucher>, IVoucherRepository
{
    private readonly CentroMaritimoDbContext _db;

    public VoucherRepository(CentroMaritimoDbContext db, ILogger<VoucherRepository> logger) : base(db, logger)
        => _db = db;

    public async Task<IReadOnlyList<Voucher>> GetPendientesByPeriodoAsync(int anio, int mes, CancellationToken ct = default)
        => await _db.Vouchers.AsNoTracking()
            .Include(v => v.Cliente)
            .Include(v => v.Barco)
            .Where(v => v.ReciboId == null && v.PeriodoAnio == anio && v.PeriodoMes == mes)
            .OrderBy(v => v.Numero)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Voucher>> GetTodosByPeriodoAsync(int anio, int mes, CancellationToken ct = default)
        => await _db.Vouchers.AsNoTracking()
            .Include(v => v.Cliente)
            .Include(v => v.Barco)
            .Include(v => v.Recibo)
            .Where(v => v.PeriodoAnio == anio && v.PeriodoMes == mes)
            .OrderBy(v => v.ClienteId).ThenBy(v => v.Numero)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Cliente>> GetClientesConVouchersPendientesAsync(int anio, int mes, CancellationToken ct = default)
    {
        var agenciaIds = await _db.Vouchers
            .Where(v => v.ReciboId == null && v.PeriodoAnio == anio && v.PeriodoMes == mes)
            .Select(v => v.ClienteId)
            .Distinct()
            .ToListAsync(ct);

        return await _db.Clientes.AsNoTracking()
            .Include(a => a.Emails)
            .Where(a => agenciaIds.Contains(a.Id))
            .OrderBy(a => a.Nombre)
            .ToListAsync(ct);
    }

    public async Task MarcarConsolidadosAsync(IEnumerable<int> voucherIds, int reciboId, CancellationToken ct = default)
    {
        var ids = voucherIds.ToList();
        var vouchers = await _db.Vouchers.Where(v => ids.Contains(v.Id)).ToListAsync(ct);
        foreach (var v in vouchers)
        {
            v.ReciboId = reciboId;
            v.UpdatedAt = DateTime.Now;
        }
        await GuardarAsync(ct);
    }

    public async Task<IReadOnlyList<Voucher>> GetPorClienteAsync(int agenciaId, int? anio = null, int? mes = null, CancellationToken ct = default)
    {
        var q = _db.Vouchers.AsNoTracking().Include(v => v.Barco).Where(v => v.ClienteId == agenciaId);
        if (anio is int a) q = q.Where(v => v.PeriodoAnio == a);
        if (mes is int m)  q = q.Where(v => v.PeriodoMes == m);
        return await q.OrderByDescending(v => v.Numero).ToListAsync(ct);
    }

    public Task<Voucher?> GetByIdConDetalleAsync(int id, CancellationToken ct = default)
        => _db.Vouchers.AsNoTracking()
            .Include(v => v.Cliente)
            .Include(v => v.Barco)
            .FirstOrDefaultAsync(v => v.Id == id, ct);
}
