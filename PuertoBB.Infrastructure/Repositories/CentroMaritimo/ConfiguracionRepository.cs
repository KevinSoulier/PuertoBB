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
        => await _db.Configuraciones.Include(c => c.PuntosDeVenta).Include(c => c.CuentasCorreo)
               .FirstOrDefaultAsync(c => c.Id == 1, ct)
           ?? throw new InvalidOperationException("La configuración singleton (Id=1) no existe.");

    public async Task<Configuracion> GetSinTrackingAsync(CancellationToken ct = default)
        => await _db.Configuraciones.AsNoTracking().Include(c => c.PuntosDeVenta).Include(c => c.CuentasCorreo)
               .FirstOrDefaultAsync(c => c.Id == 1, ct)
           ?? throw new InvalidOperationException("La configuración singleton (Id=1) no existe.");

    public async Task SaveAsync(Configuracion configuracion, CancellationToken ct = default)
    {
        configuracion.UpdatedAt = DateTime.Now;
        _db.Configuraciones.Update(configuracion);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PuntoDeVenta>> GetPuntosDeVentaAsync(CancellationToken ct = default)
        => await _db.PuntosDeVenta.AsNoTracking().OrderBy(p => p.Nombre).ToListAsync(ct);

    public async Task<PuntoDeVenta> GuardarPuntoDeVentaAsync(PuntoDeVenta puntoDeVenta, CancellationToken ct = default)
    {
        if (puntoDeVenta.Id == 0)
        {
            puntoDeVenta.ConfiguracionId = 1;
            puntoDeVenta.CreatedAt = DateTime.Now;
            _db.PuntosDeVenta.Add(puntoDeVenta);
        }
        else
        {
            puntoDeVenta.ConfiguracionId = 1;
            puntoDeVenta.UpdatedAt = DateTime.Now;
            var existing = await _db.PuntosDeVenta.FindAsync(new object[] { puntoDeVenta.Id }, ct)
                ?? throw new InvalidOperationException($"Punto de venta {puntoDeVenta.Id} no encontrado.");
            _db.Entry(existing).CurrentValues.SetValues(puntoDeVenta);
        }
        await _db.SaveChangesAsync(ct);
        return puntoDeVenta;
    }

    public async Task EliminarPuntoDeVentaAsync(int id, CancellationToken ct = default)
    {
        var pv = await _db.PuntosDeVenta.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (pv is null) return;
        _db.PuntosDeVenta.Remove(pv);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarcarPuntoDeVentaActivoAsync(int id, CancellationToken ct = default)
    {
        var todos = await _db.PuntosDeVenta.ToListAsync(ct);
        foreach (var pv in todos)
            pv.Activo = pv.Id == id;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CuentaCorreo>> GetCuentasCorreoAsync(CancellationToken ct = default)
        => await _db.CuentasCorreo.AsNoTracking().OrderBy(c => c.Nombre).ToListAsync(ct);

    public async Task<CuentaCorreo> GuardarCuentaCorreoAsync(CuentaCorreo cuenta, CancellationToken ct = default)
    {
        if (cuenta.Id == 0)
        {
            cuenta.ConfiguracionId = 1;
            cuenta.CreatedAt = DateTime.Now;
            _db.CuentasCorreo.Add(cuenta);
        }
        else
        {
            cuenta.ConfiguracionId = 1;
            cuenta.UpdatedAt = DateTime.Now;
            var existing = await _db.CuentasCorreo.FindAsync(new object[] { cuenta.Id }, ct)
                ?? throw new InvalidOperationException($"Cuenta de correo {cuenta.Id} no encontrada.");
            _db.Entry(existing).CurrentValues.SetValues(cuenta);
        }
        await _db.SaveChangesAsync(ct);
        return cuenta;
    }

    public async Task EliminarCuentaCorreoAsync(int id, CancellationToken ct = default)
    {
        var cuenta = await _db.CuentasCorreo.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cuenta is null) return;
        _db.CuentasCorreo.Remove(cuenta);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarcarCuentaCorreoActivaAsync(int id, CancellationToken ct = default)
    {
        var todas = await _db.CuentasCorreo.ToListAsync(ct);
        foreach (var c in todas)
            c.Activo = c.Id == id;
        await _db.SaveChangesAsync(ct);
    }
}
