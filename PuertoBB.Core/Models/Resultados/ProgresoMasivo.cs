namespace PuertoBB.Core.Models.Resultados;

/// <summary>
/// Avance de una operación masiva (emisión/envío por entidad): ítem actual (1-based),
/// total de ítems y nombre de la entidad que se está procesando. Modelo transitorio (no se persiste);
/// lo reporta la capa de servicio vía <see cref="System.IProgress{T}"/> para alimentar el overlay de espera.
/// </summary>
public readonly record struct ProgresoMasivo(int Actual, int Total, string Cliente);
