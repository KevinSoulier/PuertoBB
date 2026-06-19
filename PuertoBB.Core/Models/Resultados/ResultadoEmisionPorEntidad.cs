namespace PuertoBB.Core.Models.Resultados;

/// <summary>Resultado de la emisión de un recibo a una entidad (empresa/agencia) en un lote masivo o individual.</summary>
public record ResultadoEmisionPorEntidad
{
    public required int    EntidadId     { get; init; }
    public required string EntidadNombre { get; init; }
    public required bool   Exito         { get; init; }

    /// <summary>
    /// No se procesó porque ya estaba completo (emitido y enviado): es un salto benigno, NO un error.
    /// El lote masivo lo cuenta aparte para no reportar "fallida" cuando en realidad ya estaba todo hecho.
    /// </summary>
    public bool Omitido { get; init; }

    /// <summary>Número de comprobante asignado por AFIP (si Exito).</summary>
    public long? NumeroComprobante { get; init; }

    /// <summary>Error que impidió emitir (CAE rechazado, duplicado, etc.). En un omitido, lleva el motivo.</summary>
    public string? ErrorEmision { get; init; }

    /// <summary>Error de envío de mail: el recibo quedó Emitido (CAE OK) pero no se pudo enviar.</summary>
    public string? ErrorMail { get; init; }

    public static ResultadoEmisionPorEntidad Ok(int id, string nombre, long numero, string? errorMail = null)
        => new() { EntidadId = id, EntidadNombre = nombre, Exito = true, NumeroComprobante = numero, ErrorMail = errorMail };

    public static ResultadoEmisionPorEntidad Fallo(int id, string nombre, string error)
        => new() { EntidadId = id, EntidadNombre = nombre, Exito = false, ErrorEmision = error };

    /// <summary>Recibo ya completo (emitido y enviado): no se reprocesa ni se cuenta como error.
    /// Espeja <see cref="ResultadoCierrePorAgencia.Omitida"/> del flujo de cierre de período.</summary>
    public static ResultadoEmisionPorEntidad Omitida(int id, string nombre, string motivo)
        => new() { EntidadId = id, EntidadNombre = nombre, Exito = false, Omitido = true, ErrorEmision = motivo };
}
