using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Afip;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Enums;
using PuertoBB.Infrastructure.Data;

namespace CentroMaritimo.UI.Data;

/// <summary>
/// Generador de datos de stress: crea N recibos ya emitidos (con CAE) repartidos en 5 años, con una
/// mezcla realista de estados de cobro, para probar el rendimiento de filtros/paginado de "Control".
/// Inserta en lotes con el change tracker apagado. Se dispara por línea de comandos (--seed-stress N).
/// </summary>
public static class StressSeedData
{
    public static async Task<int> GenerarRecibosAsync(CentroMaritimoDbContext db, int cantidad, ILogger? log = null)
    {
        var agencias = await db.Clientes.AsNoTracking().ToListAsync();
        if (agencias.Count == 0)
            throw new InvalidOperationException("No hay agencias en la base; sembrá los datos base antes de generar recibos.");

        var rnd = new Random(20260623);
        var hoy = DateTime.Today;
        var primerMes = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-59); // 60 meses (5 años) hasta el mes actual
        var numero = await db.Recibos.MaxAsync(r => (long?)r.NumeroComprobante) ?? 0L; // continuar sin chocar el índice único

        const int pv = 1;
        const int codigoAfip = 15; // Recibo C
        const int lote = 2000;

        db.ChangeTracker.AutoDetectChangesEnabled = false;
        var generados = 0;
        for (var i = 0; i < cantidad; i++)
        {
            var ag = agencias[i % agencias.Count];
            var mes = primerMes.AddMonths(rnd.Next(0, 60));
            var emision = new DateTime(mes.Year, mes.Month, rnd.Next(1, DateTime.DaysInMonth(mes.Year, mes.Month) + 1));
            var venc = emision.AddDays(15);
            var importe = rnd.Next(50, 2000) * 1000m + rnd.Next(0, 100);
            numero++;

            var r = new Recibo
            {
                ClienteId = ag.Id,
                ReceptorNombre = ag.Nombre,
                ReceptorRazonSocial = ag.RazonSocial,
                ReceptorCuit = ag.Cuit,
                ReceptorCondicionIva = CatalogoCondicionesIvaReceptor.Descripcion(ag.CondicionIvaId),
                ReceptorCondicionIvaId = ag.CondicionIvaId,
                PeriodoAnio = mes.Year,
                PeriodoMes = mes.Month,
                Importe = importe,
                Detalle = "Recibo de prueba (stress)",
                PuntoDeVenta = pv,
                TipoComprobante = TipoComprobante.Recibo,
                CodigoAfip = codigoAfip,
                NumeroComprobante = numero,
                CAE = $"7{numero:D13}",
                FechaVencimientoCAE = emision.AddDays(10),
                FechaEmision = emision,
                FechaVencimientoPago = venc,
                EstadoFiscal = EstadoFiscal.Emitido,
                Lineas = [new ReciboLinea { Descripcion = "Servicios portuarios", Cantidad = 1, PrecioUnitario = importe, Importe = importe, Orden = 0, CreatedAt = DateTime.Now }],
                CreatedAt = DateTime.Now,
            };

            // Mezcla realista de estados de cobro (todos con CAE).
            var roll = rnd.Next(100);
            if (roll < 5)
                r.EstadoFiscal = EstadoFiscal.Anulado;                          // ~5% anulados (sin NC: dato sintético)
            else if (roll < 65)
                r.FechaPago = Menor(venc.AddDays(rnd.Next(0, 25)), hoy);         // ~60% pagados (acotado a ≤ hoy)
            else if (roll < 75)
            {
                r.FechaIncobrable = Menor(venc.AddDays(rnd.Next(30, 120)), hoy); // ~10% incobrables
                r.MotivoIncobrable = "Deuda incobrable (prueba)";
            }
            // ~25% restante: Emitido impago → Vencido o al día según venc vs hoy.

            db.Recibos.Add(r);
            generados++;

            if (generados % lote == 0)
            {
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
                log?.LogInformation("Recibos generados: {N}/{Total}", generados, cantidad);
            }
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var total = await db.Recibos.CountAsync();
        log?.LogInformation("Stress seed completo: {Generados} generados, {Total} recibos en base.", generados, total);
        return total;
    }

    /// <summary>
    /// Datos de prueba propios del Centro Marítimo (solo dev/demo, NO van en la base de producción):
    /// un catálogo de barcos genéricos + vouchers pendientes del mes corriente para probar el cierre
    /// de período y el PDF consolidado. Requiere que ya estén sembradas las agencias. Idempotente:
    /// no hace nada si la base ya tiene barcos.
    /// </summary>
    public static async Task SeedDatosDemoAsync(CentroMaritimoDbContext db, ILogger? log = null)
    {
        if (await db.Barcos.AnyAsync()) return;

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

        // Vouchers pendientes del mes corriente para probar el cierre de período.
        // Cada agencia recibe una cantidad distinta, con barcos y fechas variados para que el
        // PDF consolidado (recibo + N vouchers) muestre páginas claramente diferentes.
        var agencias = await db.Clientes.ToListAsync();
        var contador = await db.Contadores.FirstAsync(c => c.Id == 1);
        var hoy = DateTime.Today;
        var diasEnMes = DateTime.DaysInMonth(hoy.Year, hoy.Month);
        var rnd = new Random(7);
        int[] cantidades = [3, 2, 4];
        for (var ai = 0; ai < agencias.Count; ai++)
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
                    ClienteId = a.Id,
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
        log?.LogInformation("Seed demo Centro: {Barcos} barcos + vouchers del período {Mes}/{Anio}.", barcos.Length, hoy.Month, hoy.Year);
    }

    private static DateTime Menor(DateTime a, DateTime b) => a < b ? a : b;
}
