using Microsoft.EntityFrameworkCore;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Infrastructure.Data;

namespace CentroMaritimo.UI.Data;

/// <summary>Datos de ejemplo para desarrollo/demo. Solo si la base está vacía de agencias.</summary>
public static class SeedData
{
    public static async Task EnsureSeededAsync(CentroMaritimoDbContext db)
    {
        if (await db.Agencias.AnyAsync()) return;

        var grupoSocial = new GrupoFacturacion { Nombre = "Cuota Social 2026", Importe = 18000m, CreatedAt = DateTime.Now };
        db.Grupos.Add(grupoSocial);

        var agencias = new[]
        {
            Crear("Agencia Marítima Sur", "Agencia Marítima Sur S.A.", "30707654321", "ops@amsur.com.ar"),
            Crear("Naviera del Plata", "Naviera del Plata S.R.L.", "30701122334", "agencia@nplata.com.ar"),
            Crear("Consignaciones BB", "Consignaciones BB S.A.", "30705566778", "buques@cbb.com.ar"),
        };
        db.Agencias.AddRange(agencias);

        var barcos = new[]
        {
            new Barco { Nombre = "Río Paraná", CreatedAt = DateTime.Now },
            new Barco { Nombre = "Estrella del Sur", CreatedAt = DateTime.Now },
            new Barco { Nombre = "Cabo Frío", CreatedAt = DateTime.Now },
        };
        db.Barcos.AddRange(barcos);
        await db.SaveChangesAsync();

        foreach (var a in agencias)
            db.AgenciasGrupos.Add(new AgenciaGrupo { AgenciaId = a.Id, GrupoFacturacionId = grupoSocial.Id, CreatedAt = DateTime.Now });

        // Algunos vouchers pendientes del mes corriente para poder probar el cierre de período.
        var contador = await db.Contadores.FirstAsync(c => c.Id == 1);
        var hoy = DateTime.Today;
        var rnd = new Random(7);
        foreach (var a in agencias)
        {
            for (var i = 0; i < 2; i++)
            {
                contador.UltimoNumero++;
                db.Vouchers.Add(new Voucher
                {
                    AgenciaId = a.Id,
                    BarcoId = barcos[rnd.Next(barcos.Length)].Id,
                    Numero = contador.UltimoNumero,
                    Importe = rnd.Next(50, 200) * 1000m,
                    Fecha = hoy,
                    PeriodoAnio = hoy.Year,
                    PeriodoMes = hoy.Month,
                    CreatedAt = DateTime.Now
                });
            }
        }
        await db.SaveChangesAsync();
    }

    private static Agencia Crear(string nombre, string razon, string cuit, string email) => new()
    {
        Nombre = nombre,
        RazonSocial = razon,
        Cuit = cuit,
        CreatedAt = DateTime.Now,
        Emails = [new EmailAgencia { Email = email, CreatedAt = DateTime.Now }]
    };
}
