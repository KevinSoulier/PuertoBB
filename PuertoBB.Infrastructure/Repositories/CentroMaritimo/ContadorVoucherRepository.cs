using Microsoft.EntityFrameworkCore;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Infrastructure.Data;

namespace PuertoBB.Infrastructure.Repositories.CentroMaritimo;

public class ContadorVoucherRepository : IContadorVoucherRepository
{
    private readonly CentroMaritimoDbContext _db;
    public ContadorVoucherRepository(CentroMaritimoDbContext db) => _db = db;

    public async Task<ContadorVoucher> GetAsync(CancellationToken ct = default)
        => await _db.Contadores.FirstOrDefaultAsync(c => c.Id == 1, ct)
           ?? throw new InvalidOperationException("El contador de vouchers singleton (Id=1) no existe.");

    public async Task SaveAsync(ContadorVoucher contador, CancellationToken ct = default)
    {
        contador.UpdatedAt = DateTime.Now;
        _db.Contadores.Update(contador);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> ObtenerSiguienteNumeroAsync(CancellationToken ct = default)
    {
        var contador = await GetAsync(ct);
        contador.UltimoNumero++;
        contador.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        return contador.UltimoNumero;
    }
}
