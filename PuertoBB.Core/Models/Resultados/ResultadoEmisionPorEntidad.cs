namespace PuertoBB.Core.Models.Resultados;

/// <summary>Resultado de la emisión de un recibo a una entidad (empresa/agencia) en un lote masivo o individual.</summary>
public record ResultadoEmisionPorEntidad
{
    public required int    EntidadId     { get; init; }
    public required string EntidadNombre { get; init; }
    public required bool   Exito         { get; init; }

    /// <summary>Número de comprobante asignado por AFIP (si Exito).</summary>
    public long? NumeroComprobante { get; init; }

    /// <summary>Error que impidió emitir (CAE rechazado, duplicado, etc.).</summary>
    public string? ErrorEmision { get; init; }

    /// <summary>Error de envío de mail: el recibo quedó Emitido (CAE OK) pero no se pudo enviar.</summary>
    public string? ErrorMail { get; init; }

    public static ResultadoEmisionPorEntidad Ok(int id, string nombre, long numero, string? errorMail = null)
        => new() { EntidadId = id, EntidadNombre = nombre, Exito = true, NumeroComprobante = numero, ErrorMail = errorMail };

    public static ResultadoEmisionPorEntidad Fallo(int id, string nombre, string error)
        => new() { EntidadId = id, EntidadNombre = nombre, Exito = false, ErrorEmision = error };
}
