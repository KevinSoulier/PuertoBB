namespace PuertoBB.Core.Models.Mail;

/// <summary>Traduce errores SMTP conocidos a mensajes accionables para el usuario.</summary>
public static class MailErrores
{
    /// <summary>Variante por excepción: distingue un timeout (el <see cref="System.Net.Sockets"/>/MailKit
    /// cancela la operación → <see cref="OperationCanceledException"/> que NO viene del token del usuario)
    /// de una cancelación real iniciada por el usuario. Para el resto delega en <see cref="Describir(string)"/>.</summary>
    public static string Describir(Exception ex, bool canceladoPorUsuario = false)
    {
        if (!canceladoPorUsuario && ex is OperationCanceledException or TimeoutException)
            return "se agotó el tiempo de espera al enviar el correo " +
                   "(conexión lenta o adjunto grande). Probá de nuevo en unos minutos.";
        return Describir(ex.Message);
    }

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
