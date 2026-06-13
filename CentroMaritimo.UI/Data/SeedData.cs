using Microsoft.EntityFrameworkCore;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Infrastructure.Data;

namespace CentroMaritimo.UI.Data;

/// <summary>Socios reales del Centro Marítimo de Bahía Blanca. Solo si la base está vacía de agencias.</summary>
public static class SeedData
{
    public static async Task EnsureSeededAsync(CentroMaritimoDbContext db)
    {
        if (await db.Agencias.AnyAsync()) return;

        var grupoSocial = new GrupoFacturacion
        {
            Nombre = "Cuota Social 2026", Importe = 18000m, CreatedAt = DateTime.Now,
            Lineas =
            [
                Linea("Cuota societaria mensual", 1, 15000m, 0),
                Linea("Aporte sostenimiento", 1, 3000m, 1),
            ]
        };
        var grupoTablas = new GrupoFacturacion
        {
            Nombre = "Tablas de Marea", Importe = 0m, CreatedAt = DateTime.Now,
            Lineas = [Linea("Tablas de marea", 1, 0m, 0)]
        };
        db.Grupos.AddRange(grupoSocial, grupoTablas);

        // Nota: Walsh (índice 3) incluye un ítem extra "Aporte Trans Ona" en su cuota social — gestionar por UI.
        // Donmar (índice 6) solo paga Tablas de Marea, 2 veces al año — no integra Cuota Social.
        var agencias = new[]
        {
            /* 0 */ Crear("ADM Agro",                   "ADM Agro S.R.L.",                                       "30621973173", "Lucas.Majnach2@adm.com"),
            /* 1 */ Crear("Ag. Marítima Austral",       "Agencia Marítima Austral S.R.L.",                       "30643949381", "operaciones@agencia-austral.com.ar"),
            /* 2 */ Crear("Ag. Marítima Internacional", "Agencia Marítima Internacional S.A.",                   "30585343427", "ltorres@ocean.com.ar"),
            /* 3 */ Crear("Agencia Marítima Walsh",     "Agencia Marítima Walsh E Burton S.R.L.",                "30506738128", "adminis@walsh.com.ar"),
            /* 4 */ Crear("Asoc. Cooperativas Arg.",    "Asociación de Cooperativas Argentinas Coop. Ltda.",     "30500120882", "bbaprovedores@acacoop.com.ar"),
            /* 5 */ Crear("Cargill",                    "Cargill Soc. Anón. Com. e Industrial",                 "30506792165", "Melanie_Pagnanelli@cargill.com"),
            /* 6 */ Crear("Donmar",                     "Donmar S.A.",                                           "30680766610", "fmezzano@serviciosmaritimos.com"),
            /* 7 */ Crear("Fertimport",                 "Fertimport S.A.",                                       "30707691847", "bar.fertimport.adm@bunge.com"),
            /* 8 */ Crear("Maritime Shipping Agency",   "Maritime Shipping Agency S.R.L.",                       "30709247235", "nlamonega@isa-agents.com.ar"),
            /* 9 */ Crear("Puerto White Multimodal",    "Puerto White Multimodal S.A.",                          "30711320896", "administracion@puertowhite.com.ar"),
            /*10 */ Crear("Sea White",                  "Sea White S.A.",                                        "30707232338", "accounting@seawhite.com.ar"),
            /*11 */ Crear("Terminal Bahía Blanca",      "Terminal Bahía Blanca S.A.",                            "30660168105", "natalia.sola@bunge.com"),
            /*12 */ Crear("United Seas",                "United Seas S.R.L.",                                    "30647178908", "leticia.alza@moggia.com.ar", "sandra.mishevitch@unitedseas.com.ar"),
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

        // Cuota Social: todos excepto Donmar (índice 6)
        foreach (var idx in new[] { 0, 1, 2, 3, 4, 5, 7, 8, 9, 10, 11, 12 })
            db.AgenciasGrupos.Add(new AgenciaGrupo { AgenciaId = agencias[idx].Id, GrupoFacturacionId = grupoSocial.Id, CreatedAt = DateTime.Now });

        // Tablas de Marea: Austral, Internacional, Walsh, Donmar, Fertimport, Maritime Shipping, Sea White
        foreach (var idx in new[] { 1, 2, 3, 6, 7, 8, 10 })
            db.AgenciasGrupos.Add(new AgenciaGrupo { AgenciaId = agencias[idx].Id, GrupoFacturacionId = grupoTablas.Id, CreatedAt = DateTime.Now });

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

    private static Agencia Crear(string nombre, string razon, string cuit, params string[] emails) => new()
    {
        Nombre = nombre,
        RazonSocial = razon,
        Cuit = cuit,
        CondicionIvaId = 1, // IVA Responsable Inscripto (dato demo; verificar con "Validar CUIT en ARCA")
        CreatedAt = DateTime.Now,
        Emails = emails.Select(e => new EmailAgencia { Email = e, CreatedAt = DateTime.Now }).ToList()
    };

    private static GrupoFacturacionLinea Linea(string descripcion, decimal cantidad, decimal precioUnitario, int orden) => new()
    {
        Descripcion = descripcion,
        Cantidad = cantidad,
        PrecioUnitario = precioUnitario,
        Importe = cantidad * precioUnitario,
        Orden = orden,
        CreatedAt = DateTime.Now
    };
}
