namespace PuertoBB.Services.Afip;

/// <summary>
/// Traduce las observaciones/errores crudos de AFIP a mensajes accionables para el usuario.
/// Los códigos llegan con el formato "[codigo] mensaje" desde el mapeo de WSFE.
/// </summary>
internal static class AfipErrores
{
    public static string Describir(string? observaciones)
    {
        if (string.IsNullOrWhiteSpace(observaciones))
            return "AFIP no devolvió detalle del rechazo.";

        var hints = new List<string>();
        if (observaciones.Contains("[10071]")) hints.Add("No corresponde informar IVA en comprobantes tipo C.");
        if (observaciones.Contains("[10016]")) hints.Add("La fecha del comprobante está fuera del rango permitido por AFIP.");
        if (observaciones.Contains("[10015]")) hints.Add("Revise el período de servicio y el vencimiento de pago (Concepto Servicios).");
        if (observaciones.Contains("[600]"))   hints.Add("Credenciales inválidas: revise el certificado y que el servicio 'wsfe' esté habilitado para el CUIT.");

        return hints.Count > 0 ? $"{observaciones} — {string.Join(" ", hints)}" : observaciones;
    }
}
