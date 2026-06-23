using PuertoBB.Core.Enums;

namespace PuertoBB.Core.Models.Resultados;

/// <summary>
/// Filtro + paginación de la sección "Control". El estado se traduce a predicados de columna en el
/// repositorio (paginado server-side). La búsqueda de texto, cuando hay texto, se resuelve en memoria
/// contra los campos formateados de la grilla (ver <c>ControlBusqueda</c>).
/// </summary>
public record FiltroControlPagos
{
    public FiltroEstadoControl Estado { get; init; } = FiltroEstadoControl.PendientesDePago;

    public string? Texto { get; init; }

    /// <summary>Página 1-based.</summary>
    public int Pagina        { get; init; } = 1;
    public int TamanioPagina { get; init; } = 100;
}
