namespace PuertoBB.Core.Models.Resultados;

/// <summary>
/// Una página de resultados de una consulta paginada. <see cref="Total"/> es el total de filas que
/// matchean el filtro (no solo las de la página); <see cref="Vencidos"/> es un contador auxiliar para
/// el resumen de cobranza.
/// </summary>
public record PaginaResultado<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Vencidos,
    int Pagina,
    int TamanioPagina)
{
    public int TotalPaginas => TamanioPagina > 0
        ? Math.Max(1, (int)Math.Ceiling((double)Total / TamanioPagina))
        : 1;
}
