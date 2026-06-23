using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Afip;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Enums;
using PuertoBB.Infrastructure.Data;

namespace CamaraPortuaria.UI.Data;

/// <summary>
/// Generador de datos de stress: crea N recibos ya emitidos (con CAE) repartidos en 5 años, con una
/// mezcla realista de estados de cobro, para probar el rendimiento de filtros/paginado de "Control".
/// Inserta en lotes con el change tracker apagado. Se dispara por línea de comandos (--seed-stress N).
/// </summary>
public static class StressSeedData
{
    public static async Task<int> GenerarRecibosAsync(CamaraPortuariaDbContext db, int cantidad, ILogger? log = null)
    {
        var empresas = await db.Empresas.AsNoTracking().ToListAsync();
        if (empresas.Count == 0)
            throw new InvalidOperationException("No hay empresas en la base; sembrá los datos base antes de generar recibos.");

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
            var em = empresas[i % empresas.Count];
            var mes = primerMes.AddMonths(rnd.Next(0, 60));
            var emision = new DateTime(mes.Year, mes.Month, rnd.Next(1, DateTime.DaysInMonth(mes.Year, mes.Month) + 1));
            var venc = emision.AddDays(15);
            var importe = rnd.Next(50, 2000) * 1000m + rnd.Next(0, 100);
            numero++;

            var r = new Recibo
            {
                EmpresaId = em.Id,
                ReceptorNombre = em.Nombre,
                ReceptorRazonSocial = em.RazonSocial,
                ReceptorCuit = em.Cuit,
                ReceptorCondicionIva = CatalogoCondicionesIvaReceptor.Descripcion(em.CondicionIvaId),
                ReceptorCondicionIvaId = em.CondicionIvaId,
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

    private static DateTime Menor(DateTime a, DateTime b) => a < b ? a : b;
}
