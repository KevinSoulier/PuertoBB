using PuertoBB.Core.Entities.Common;

namespace PuertoBB.Core.Entities;

/// <summary>
/// Detalle/concepto reutilizable para los recibos (autocompletado en la emisión individual).
/// Índice único: (Nombre).
/// </summary>
public class ConceptoRecibo : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;
}
