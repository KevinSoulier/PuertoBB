namespace PuertoBB.Core.Enums;

/// <summary>
/// Eje de envío del mail. NO se persiste: se deriva de FechaEnvioMail/UltimoErrorMail.
/// </summary>
public enum EstadoEnvio
{
    NoEnviado,
    Enviado,
    Fallido
}
