namespace PuertoBB.Core.Models.Mail;

/// <summary>Traduce errores SMTP conocidos a mensajes accionables para el usuario.</summary>
public static class MailErrores
{
    /// <summary>Devuelve un mensaje accionable para errores SMTP conocidos, o el mensaje original.
    /// El caso típico es Microsoft/Outlook rechazando la autenticación básica (535 5.7.139): hay que pasar a OAuth2.</summary>
    public static string Describir(string mensaje)
    {
        if (mensaje.Contains("5.7.139")
            || mensaje.Contains("basic authentication is disabled", StringComparison.OrdinalIgnoreCase)
            || mensaje.Contains("SmtpClientAuthentication", StringComparison.OrdinalIgnoreCase))
            return "tu proveedor (Microsoft/Outlook) deshabilitó la autenticación básica. " +
                   "En Configuración → Correo cambiá la autenticación a OAuth2.";
        return mensaje;
    }
}
