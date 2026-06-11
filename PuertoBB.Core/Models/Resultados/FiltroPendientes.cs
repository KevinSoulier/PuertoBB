using PuertoBB.Core.Enums;

namespace PuertoBB.Core.Models.Resultados;

/// <summary>Filtros del dashboard de pendientes / control de pagos.</summary>
public record FiltroPendientes
{
    public int?          PeriodoAnio        { get; init; }
    public int?          PeriodoMes         { get; init; }
    public int?          GrupoFacturacionId { get; init; }
    public int?          EntidadId          { get; init; }
    public ReciboEstado? Estado             { get; init; }
    public bool          SoloVencidos       { get; init; }
    public bool          ExcluirMorosos     { get; init; } = true;
}
