using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Common;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Mail;

namespace PuertoBB.Services.Mail.Oauth;

/// <summary>Obtiene access tokens OAuth2 para SMTP (XOAUTH2). Registrado como Singleton: cachea el token
/// en memoria y lo renueva al expirar. Soporta client_credentials (flujo Cliente) y refresh_token (flujo Interactivo).</summary>
public sealed class OAuthTokenProvider : IMailTokenProvider
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly ConcurrentDictionary<string, (string Token, DateTimeOffset Expira)> _cache = new();
    private readonly ILogger<OAuthTokenProvider> _logger;

    public OAuthTokenProvider(ILogger<OAuthTokenProvider> logger) => _logger = logger;

    public async Task<ServiceResult<string>> GetAccessTokenAsync(MailConfig config, CancellationToken ct = default)
    {
        // Loguea la falla a Warning (el FileLogger escribe Warning+ siempre) y devuelve el resultado.
        ServiceResult<string> Fail(string mensaje, Exception? ex = null)
        {
            _logger.LogWarning(ex, "Token OAuth ({Proveedor}/{Flujo}) falló: {Mensaje}",
                config.OAuthProveedor, config.OAuthFlujo, mensaje);
            return ServiceResult<string>.Fail(mensaje);
        }

        if (string.IsNullOrWhiteSpace(config.OAuthClientId))
            return Fail("Falta el Client ID de OAuth2.");

        var endpoints = OAuthPresets.Resolver(config);
        if (string.IsNullOrWhiteSpace(endpoints.TokenEndpoint))
            return Fail("No se pudo determinar el endpoint de token OAuth2.");

        var clave = ClaveCache(config, endpoints);
        if (_cache.TryGetValue(clave, out var cached) && cached.Expira > DateTimeOffset.UtcNow.AddSeconds(60))
            return ServiceResult<string>.Ok(cached.Token);

        var form = ConstruirForm(config, endpoints);
        if (form is null)
            return Fail(config.OAuthFlujo == OAuthFlujo.Interactivo
                ? "No hay una sesión OAuth2 iniciada. Usá «Iniciar sesión…» en Configuración → Correo."
                : "Faltan credenciales de cliente OAuth2 (Client Secret).");

        try
        {
            var r = await OAuthHelpers.PostTokenAsync(Http, endpoints.TokenEndpoint, form, ct);
            if (!string.IsNullOrWhiteSpace(r.Error) || string.IsNullOrWhiteSpace(r.AccessToken))
                return Fail($"OAuth2 rechazó la solicitud de token: {r.ErrorDescription ?? r.Error ?? "respuesta sin access_token"}.");

            _cache[clave] = (r.AccessToken!, DateTimeOffset.UtcNow.AddSeconds(r.ExpiresIn));
            return ServiceResult<string>.Ok(r.AccessToken!);
        }
        catch (Exception ex)
        {
            return Fail($"No se pudo obtener el token OAuth2: {ex.Message}", ex);
        }
    }

    /// <summary>Arma el cuerpo del request de token. Devuelve null si faltan credenciales según el flujo.</summary>
    private static List<KeyValuePair<string, string>>? ConstruirForm(MailConfig config, OAuthEndpoints endpoints)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("client_id", config.OAuthClientId!.Trim()),
            new("scope", endpoints.Scope),
        };
        if (!string.IsNullOrWhiteSpace(config.OAuthClientSecret))
            form.Add(new("client_secret", config.OAuthClientSecret.Trim()));

        if (config.OAuthFlujo == OAuthFlujo.Cliente)
        {
            if (string.IsNullOrWhiteSpace(config.OAuthClientSecret)) return null;
            form.Add(new("grant_type", "client_credentials"));
        }
        else
        {
            if (string.IsNullOrWhiteSpace(config.OAuthRefreshToken)) return null;
            form.Add(new("grant_type", "refresh_token"));
            form.Add(new("refresh_token", config.OAuthRefreshToken.Trim()));
        }
        return form;
    }

    private static string ClaveCache(MailConfig config, OAuthEndpoints endpoints)
    {
        var secreto = config.OAuthFlujo == OAuthFlujo.Cliente ? config.OAuthClientSecret : config.OAuthRefreshToken;
        return string.Join('|', config.OAuthFlujo, config.OAuthClientId, endpoints.TokenEndpoint, endpoints.Scope, secreto?.GetHashCode());
    }
}
