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
        if (observaciones.Contains("[10044]")) hints.Add("En comprobantes tipo C el importe exento (ImpOpEx) debe ser 0: es un problema de cálculo del comprobante.");
        if (observaciones.Contains("[10048]")) hints.Add("El importe total no coincide con el neto declarado (ImpTotal ≠ ImpNeto + ImpTrib): es un problema de cálculo del comprobante.");
        if (observaciones.Contains("[10016]")) hints.Add("La fecha del comprobante está fuera del rango permitido por AFIP.");
        if (observaciones.Contains("[10015]")) hints.Add("Revise el período de servicio y el vencimiento de pago (Concepto Servicios).");
        if (observaciones.Contains("[600]"))
        {
            if (observaciones.Contains("lista de relaciones") || observaciones.Contains("ValidacionDeToken"))
                hints.Add("El certificado autenticó, pero ese CUIT no figura en sus relaciones para 'wsfe'. Verifique que el CUIT emisor sea correcto (dígito verificador válido, sin errores de tipeo) y que coincida con el del certificado; y que 'wsfe' esté autorizado a ese certificado para ese CUIT en el mismo ambiente (producción: Administrador de Relaciones; homologación: WSASS).");
            else
                hints.Add("Credenciales inválidas: revise el certificado y que el servicio 'wsfe' esté habilitado para el CUIT.");
        }
        if (observaciones.Contains("[10242]") || observaciones.Contains("[10243]") || observaciones.Contains("[10246]"))
            hints.Add("La condición frente al IVA del receptor falta o no es válida para este comprobante (RG 5616): asígnela en el ABM de empresas/agencias.");

        return hints.Count > 0 ? $"{observaciones} — {string.Join(" ", hints)}" : observaciones;
    }
}
