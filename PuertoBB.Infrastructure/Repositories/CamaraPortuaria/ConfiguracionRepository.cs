using Microsoft.EntityFrameworkCore;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;
using PuertoBB.Infrastructure.Data;

namespace PuertoBB.Infrastructure.Repositories.CamaraPortuaria;

public class ConfiguracionRepository : IConfiguracionRepository
{
    private readonly CamaraPortuariaDbContext _db;
    public ConfiguracionRepository(CamaraPortuariaDbContext db) => _db = db;

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
