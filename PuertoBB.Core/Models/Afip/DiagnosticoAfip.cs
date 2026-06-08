namespace PuertoBB.Core.Models.Afip;

/// <summary>
/// Resultado del diagnóstico de conexión con AFIP (botón "Probar conexión"):
/// estado del servicio (FEDummy) y de la autenticación con el certificado (login WSAA + WSFE).
/// </summary>
public record DiagnosticoAfip
{
    /// <summary>FEDummy respondió OK (no requiere certificado).</summary>
    public required bool ServicioOk { get; init; }

    /// <summary>El certificado autenticó correctamente y el servicio wsfe está habilitado.</summary>
    public required bool AutenticacionOk { get; init; }

    /// <summary>Último número autorizado para el punto de venta/tipo consultado (si la autenticación funcionó).</summary>
    public long? UltimoComprobante { get; init; }

    /// <summary>Detalle legible del diagnóstico (éxitos y errores).</summary>
    public string? Detalle { get; init; }
}
