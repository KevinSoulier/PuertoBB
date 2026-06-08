namespace Afip.Wsfe;

/// <summary>Resultado de una solicitud de CAE a WSFE.</summary>
public sealed record AfipCaeResult
{
    /// <summary>true si AFIP aprobó el comprobante (Resultado = "A") y devolvió un CAE.</summary>
    public required bool Aprobado { get; init; }

    public string?   Cae                 { get; init; }
    public DateTime? FechaVencimientoCae { get; init; }
    public long      Numero              { get; init; }

    /// <summary>Errores/observaciones de AFIP (códigos + mensajes) cuando el comprobante es rechazado u observado.</summary>
    public string?   Observaciones       { get; init; }
}
