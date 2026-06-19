namespace PuertoBB.Core.Models.Resultados;

/// <summary>
/// Estado de emisión de una entidad (empresa/agencia) miembro de un grupo, para un período.
/// Alimenta la tabla única de emisión masiva: si <see cref="Recibo"/> es null, la entidad aún
/// no tiene recibo en el período ("No emitido"); si no, lleva el recibo con su estado real.
/// <see cref="ImporteEsperado"/> es el total que tendría el recibo según el grupo, para poder
/// validar el monto ANTES de emitir (se muestra en la columna Importe de los no emitidos).
/// </summary>
/// <typeparam name="TRecibo">El tipo de Recibo de la app (Cámara Portuaria o Centro Marítimo).</typeparam>
public record EstadoEmisionEntidad<TRecibo>(int EntidadId, string EntidadNombre, TRecibo? Recibo, decimal ImporteEsperado)
    where TRecibo : class;
