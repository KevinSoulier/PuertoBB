namespace PuertoBB.Core.Models.Mail;

/// <summary>Flujo OAuth2 para obtener el access token.</summary>
public enum OAuthFlujo
{
    /// <summary>Authorization code + PKCE: el usuario consiente una vez en el navegador y se guarda un
    /// refresh token. Sirve para cuentas de empresa y personales (Microsoft, Google).</summary>
    Interactivo = 0,
    /// <summary>Client credentials: la app se autentica con Client ID + Secret + Tenant, sin navegador.
    /// Solo para casillas Microsoft 365 de empresa administradas por el usuario.</summary>
    Cliente = 1
}
