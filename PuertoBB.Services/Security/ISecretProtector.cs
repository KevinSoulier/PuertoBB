namespace PuertoBB.Services.Security;

/// <summary>
/// Protege/recupera secretos para almacenarlos en reposo (ej. la contraseña del certificado AFIP).
/// </summary>
public interface ISecretProtector
{
    /// <summary>Devuelve el valor protegido para guardar en la base. Null/empty pasa sin cambios.</summary>
    string? Protect(string? plaintext);

    /// <summary>Recupera el texto plano a partir del valor almacenado. Tolera valores legados en texto plano.</summary>
    string? Unprotect(string? stored);
}
