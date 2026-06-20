using PuertoBB.Core.Enums;

namespace PuertoBB.Core.Models.Resultados;

/// <summary>
/// Filtro + paginación de la sección "Control" (paginado server-side). El estado se traduce a
/// predicados de columna en el repositorio; la búsqueda de texto va contra columnas clave (nombre del
/// receptor, n° de comprobante, CAE).
/// </summary>
public record FiltroControlPagos
{
    public FiltroEstadoControl Estado { get; init; } = FiltroEstadoControl.PendientesDePago;

    /// <summary>Solo aplican cuando <see cref="Estado"/> es <see cref="FiltroEstadoControl.PendientesDePago"/>.</summary>
    public bool SoloVencidos       { get; init; }
    public bool IncluirIncobrables { get; init; }

    public string? Texto { get; init; }

    /// <summary>Página 1-based.</summary>
    public int Pagina        { get; init; } = 1;
    public int TamanioPagina { get; init; } = 100;
}
