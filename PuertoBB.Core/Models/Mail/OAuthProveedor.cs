namespace PuertoBB.Core.Models.Mail;

/// <summary>Proveedor OAuth2. Define los endpoints y scopes por defecto (ver OAuthPresets).</summary>
public enum OAuthProveedor
{
    /// <summary>Microsoft 365 (empresa): host smtp.office365.com.</summary>
    Microsoft     = 0,
    Google        = 1,
    /// <summary>Endpoints y scope cargados a mano por el usuario.</summary>
    Personalizado = 2,
    /// <summary>Cuenta personal Outlook.com/Hotmail/Live: mismos endpoints que Microsoft pero
    /// host SMTP smtp-mail.outlook.com.</summary>
    OutlookPersonal = 3
}
