namespace PuertoBB.Core.Models.Resultados;

/// <summary>Resultado de la anulación de un recibo: datos de la nota de crédito emitida.</summary>
public record ResultadoAnulacion
{
    public required int  PuntoDeVenta      { get; init; }
    public required long NumeroComprobante { get; init; }

    /// <summary>Error de envío de mail: la NC quedó emitida pero no se pudo enviar. Null si se envió o no se pidió.</summary>
    public string? ErrorMail { get; init; }

    public static ResultadoAnulacion Ok(int puntoDeVenta, long numero, string? errorMail = null)
        => new() { PuntoDeVenta = puntoDeVenta, NumeroComprobante = numero, ErrorMail = errorMail };
}
