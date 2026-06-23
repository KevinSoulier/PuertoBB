using PuertoBB.Core.Common;
using PuertoBB.Core.Models.Resultados;

namespace PuertoBB.Services.Common;

/// <summary>
/// Búsqueda de texto de la sección "Control", en memoria y <b>fiel a lo que se ve en la grilla</b>:
/// matchea contra los mismos campos formateados que muestra cada fila (agencia/empresa, comprobante,
/// período, importe, emisión y vencimiento), usando <see cref="Formato"/> como única fuente de formato.
/// Se hace en memoria porque esos formatos (es-AR: "Junio 2026", "$ 1.000,00", "0001-00000094",
/// "22/06/2026") no se pueden reproducir fielmente en SQL de SQLite.
/// </summary>
public static class ControlBusqueda
{
    /// <summary>Texto buscable de un recibo: las 6 columnas visibles, tal como se muestran, en minúsculas.</summary>
    public static string TextoBusqueda(IReciboBusquedaView r)
        => string.Join(' ',
                r.ReceptorNombre,
                Formato.Comprobante(r.PuntoDeVenta, r.NumeroComprobante),
                Formato.Periodo(r.PeriodoAnio, r.PeriodoMes),
                Formato.Moneda(r.Importe),
                Formato.Fecha(r.FechaEmision),
                Formato.Fecha(r.FechaVencimientoPago))
            .ToLowerInvariant();

    /// <summary>
    /// Filtra los candidatos (ya ordenados y filtrados por estado) por el texto, cuenta total/vencidos
    /// y devuelve la página pedida. Sin texto, devuelve los candidatos paginados tal cual.
    /// </summary>
    public static PaginaResultado<T> Filtrar<T>(
        IReadOnlyList<T> candidatos, string? texto, int pagina, int tamanio, DateTime hoy)
        where T : IReciboBusquedaView
    {
        var buscado = (texto ?? string.Empty).Trim().ToLowerInvariant();
        var filtrados = string.IsNullOrEmpty(buscado)
            ? candidatos
            : candidatos.Where(r => TextoBusqueda(r).Contains(buscado)).ToList();

        var total = filtrados.Count;
        var vencidos = filtrados.Count(r => EstadoReciboHelper.EstaVencido(r, hoy));

        var tam = tamanio > 0 ? tamanio : 100;
        var totalPaginas = Math.Max(1, (int)Math.Ceiling((double)total / tam));
        var pag = Math.Clamp(pagina, 1, totalPaginas);

        var items = filtrados.Skip((pag - 1) * tam).Take(tam).ToList();
        return new PaginaResultado<T>(items, total, vencidos, pag, tam);
    }
}
