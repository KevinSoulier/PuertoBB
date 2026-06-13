using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PuertoBB.Services.Mail.Oauth;

/// <summary>Utilidades compartidas por el proveedor de tokens y el flujo interactivo OAuth2.</summary>
internal static class OAuthHelpers
{
    internal sealed record TokenRespuesta(
        string? AccessToken, string? RefreshToken, string? IdToken, int ExpiresIn,
        string? Error, string? ErrorDescription);

    /// <summary>POST application/x-www-form-urlencoded al token endpoint y parsea la respuesta JSON.</summary>
    internal static async Task<TokenRespuesta> PostTokenAsync(
        HttpClient http, string tokenEndpoint,
        IEnumerable<KeyValuePair<string, string>> form, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(form);
        using var resp = await http.PostAsync(tokenEndpoint, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        var root = doc.RootElement;
        return new TokenRespuesta(
            Str(root, "access_token"),
            Str(root, "refresh_token"),
            Str(root, "id_token"),
            root.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var s) ? s : 3600,
            Str(root, "error"),
            Str(root, "error_description"));
    }

    private static string? Str(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    /// <summary>Genera (code_verifier, code_challenge) para PKCE (S256).</summary>
    internal static (string Verifier, string Challenge) GenerarPkce()
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    internal static string TokenAleatorio() => Base64Url(RandomNumberGenerator.GetBytes(16));

    internal static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Extrae el email/usuario del payload de un id_token JWT (sin validar la firma; solo para mostrar).</summary>
    internal static string? EmailDesdeIdToken(string? idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken)) return null;
        var partes = idToken.Split('.');
        if (partes.Length < 2) return null;
        try
        {
            var payload = partes[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
            var root = doc.RootElement;
            return Str(root, "email") ?? Str(root, "preferred_username") ?? Str(root, "upn");
        }
        catch { return null; }
    }
}
