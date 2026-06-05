using Microsoft.EntityFrameworkCore;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Infrastructure.Data;

namespace PuertoBB.Infrastructure.Repositories.CentroMaritimo;

public class ConfiguracionRepository : IConfiguracionRepository
{
    private readonly CentroMaritimoDbContext _db;
    public ConfiguracionRepository(CentroMaritimoDbContext db) => _db = db;

    public async Task<Configuracion> GetAsync(CancellationToken ct = default)
        => await _db.Configuraciones.FirstOrDefaultAsync(c => c.Id == 1, ct)
           ?? throw new InvalidOperationException("La configuración singleton (Id=1) no existe.");

    public async Task SaveAsync(Configuracion configuracion, CancellationToken ct = default)
    {
        configuracion.UpdatedAt = DateTime.Now;
        _db.Configuraciones.Update(configuracion);
        await _db.SaveChangesAsync(ct);
    }
}
