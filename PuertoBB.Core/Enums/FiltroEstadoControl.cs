namespace PuertoBB.Core.Enums;

/// <summary>
/// Opción de estado del filtro de la sección "Control" (paginado server-side). Cada valor se traduce
/// a un predicado de columna en el repositorio; debe mantenerse en sync con
/// <see cref="Common.EstadoReciboHelper.EtiquetaEstado"/> (única fuente de la etiqueta mostrada).
/// </summary>
public enum FiltroEstadoControl
{
    /// <summary>Vista de cobranza por defecto: emitidos por cobrar (modulada por los checkboxes).</summary>
    PendientesDePago,
    Emitido,
    Vencido,
    Pagado,
    Incobrable,
    Anulado,
    Todos
}
