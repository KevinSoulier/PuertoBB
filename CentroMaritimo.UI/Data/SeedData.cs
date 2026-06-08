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
            new Barco { Nombre = "Don Pedro", CreatedAt = DateTime.Now },
            new Barco { Nombre = "Mar Argentino", CreatedAt = DateTime.Now },
        };
        db.Barcos.AddRange(barcos);
        await db.SaveChangesAsync();

        foreach (var a in agencias)
            db.AgenciasGrupos.Add(new AgenciaGrupo { AgenciaId = a.Id, GrupoFacturacionId = grupoSocial.Id, CreatedAt = DateTime.Now });

        // Vouchers pendientes del mes corriente para probar el cierre de período.
        // Cada agencia recibe una cantidad distinta, con barcos y fechas variados para que el
        // PDF consolidado (recibo + N vouchers) muestre páginas claramente diferentes.
        var contador = await db.Contadores.FirstAsync(c => c.Id == 1);
        var hoy = DateTime.Today;
        var diasEnMes = DateTime.DaysInMonth(hoy.Year, hoy.Month);
        var rnd = new Random(7);
        int[] cantidades = [3, 2, 4];
        for (var ai = 0; ai < agencias.Length; ai++)
        {
            var a = agencias[ai];
            var cantidad = cantidades[ai % cantidades.Length];

            // Barcos distintos por agencia y días distintos dentro del mes (orden cronológico).
            var barcosAgencia = barcos.OrderBy(_ => rnd.Next()).Take(cantidad).ToList();
            var dias = Enumerable.Range(1, diasEnMes).OrderBy(_ => rnd.Next()).Take(cantidad).OrderBy(d => d).ToList();

            for (var i = 0; i < cantidad; i++)
            {
                contador.UltimoNumero++;
                db.Vouchers.Add(new Voucher
                {
                    AgenciaId = a.Id,
                    BarcoId = barcosAgencia[i].Id,
                    Numero = contador.UltimoNumero,
                    Importe = rnd.Next(50, 200) * 1000m,
                    Fecha = new DateTime(hoy.Year, hoy.Month, dias[i]),
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
