using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Common;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Mail;

namespace PuertoBB.Services.Mail.Oauth;

/// <summary>Consentimiento interactivo OAuth2: authorization code + PKCE con redirect a un loopback local.
/// Abre el navegador del sistema y espera el callback; devuelve el refresh token + el email autenticado.</summary>
public sealed class OAuthInteractiveFlow : IOAuthInteractiveFlow
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly TimeSpan EsperaMaxima = TimeSpan.FromMinutes(3);
    private readonly ILogger<OAuthInteractiveFlow> _logger;

    public OAuthInteractiveFlow(ILogger<OAuthInteractiveFlow> logger) => _logger = logger;

    public async Task<ServiceResult<OAuthResultado>> AutenticarAsync(MailConfig config, CancellationToken ct = default)
    {
        // Loguea la falla a Warning (el FileLogger escribe Warning+ siempre) y devuelve el resultado.
        ServiceResult<OAuthResultado> Fail(string mensaje, Exception? ex = null)
        {
            _logger.LogWarning(ex, "Login OAuth ({Proveedor}/{Flujo}) falló: {Mensaje}",
                config.OAuthProveedor, config.OAuthFlujo, mensaje);
            return ServiceResult<OAuthResultado>.Fail(mensaje);
        }

        if (string.IsNullOrWhiteSpace(config.OAuthClientId))
            return Fail("Indicá el Client ID antes de iniciar sesión.");

        var endpoints = OAuthPresets.Resolver(config);
        if (string.IsNullOrWhiteSpace(endpoints.AuthorizeEndpoint) || string.IsNullOrWhiteSpace(endpoints.TokenEndpoint))
            return Fail("No se pudieron determinar los endpoints OAuth2.");

        var (verifier, challenge) = OAuthHelpers.GenerarPkce();
        var state = OAuthHelpers.TokenAleatorio();
        var redirectUri = $"http://localhost:{PuertoLibre()}/";

        var listener = new HttpListener();
        listener.Prefixes.Add(redirectUri);
        try { listener.Start(); }
        catch (Exception ex) { return Fail($"No se pudo abrir el escucha local: {ex.Message}", ex); }

        _logger.LogInformation("Login OAuth iniciado ({Proveedor}/{Flujo}). authorize={Authorize} redirect_uri={Redirect} scope={Scope}",
            config.OAuthProveedor, config.OAuthFlujo, endpoints.AuthorizeEndpoint, redirectUri, endpoints.Scope);

        try
        {
            var authorizeUrl = ConstruirAuthorizeUrl(config, endpoints, redirectUri, challenge, state);
            try { Process.Start(new ProcessStartInfo(authorizeUrl) { UseShellExecute = true }); }
            catch (Exception ex) { return Fail($"No se pudo abrir el navegador: {ex.Message}", ex); }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(EsperaMaxima);
            await using var reg = timeoutCts.Token.Register(() => { try { if (listener.IsListening) listener.Stop(); } catch { /* ya cerrado */ } });

            HttpListenerContext context;
            try { context = await listener.GetContextAsync(); }
            catch when (timeoutCts.IsCancellationRequested)
            {
                // Si el navegador mostró un error (p. ej. redirect_uri/scope no válidos) el proveedor NO redirige
                // al loopback, así que esto se ve como un timeout. El error real queda en la pantalla del navegador.
                return Fail("No se recibió la respuesta del navegador (timeout). Si el navegador mostró un error de " +
                            "redirect_uri o scope, registrá el redirect y revisá el scope, y volvé a intentar. " +
                            $"redirect_uri usado: {redirectUri}");
            }

            var query = context.Request.QueryString;
            await ResponderNavegadorAsync(context);

            if (query["error"] is { } err)
            {
                _logger.LogWarning("El proveedor OAuth devolvió un error en el callback. error={Error} description={Desc} redirect_uri={Redirect}",
                    err, query["error_description"], redirectUri);
                return ServiceResult<OAuthResultado>.Fail($"El proveedor rechazó la autorización: {query["error_description"] ?? err}.");
            }
            if (query["state"] != state)
                return Fail("La respuesta de autorización no coincide (state inválido).");
            var code = query["code"];
            if (string.IsNullOrWhiteSpace(code))
                return Fail("El proveedor no devolvió un código de autorización.");

            var form = new List<KeyValuePair<string, string>>
            {
                new("client_id", config.OAuthClientId!.Trim()),
                new("grant_type", "authorization_code"),
                new("code", code!),
                new("redirect_uri", redirectUri),
                new("code_verifier", verifier),
            };
            if (!string.IsNullOrWhiteSpace(config.OAuthClientSecret))
                form.Add(new("client_secret", config.OAuthClientSecret.Trim()));

            var r = await OAuthHelpers.PostTokenAsync(Http, endpoints.TokenEndpoint, form, ct);
            if (!string.IsNullOrWhiteSpace(r.Error) || string.IsNullOrWhiteSpace(r.AccessToken))
                return Fail($"No se pudo canjear el código: {r.ErrorDescription ?? r.Error ?? "respuesta inválida"}.");
            if (string.IsNullOrWhiteSpace(r.RefreshToken))
                return Fail("El proveedor no devolvió un refresh token. Verificá que el scope incluya acceso offline.");

            var usuario = OAuthHelpers.EmailDesdeIdToken(r.IdToken) ?? config.OAuthUsuario ?? config.EmailRemitente;
            _logger.LogInformation("Login OAuth OK ({Proveedor}/{Flujo}) como {Usuario}.", config.OAuthProveedor, config.OAuthFlujo, usuario);
            return ServiceResult<OAuthResultado>.Ok(new OAuthResultado(r.RefreshToken!, usuario));
        }
        catch (Exception ex)
        {
            return Fail($"Falló la autenticación interactiva: {ex.Message}", ex);
        }
        finally
        {
            if (listener.IsListening) listener.Stop();
            listener.Close();
        }
    }

    private static string ConstruirAuthorizeUrl(MailConfig config, OAuthEndpoints endpoints, string redirectUri, string challenge, string state)
    {
        var q = new Dictionary<string, string?>
        {
            ["client_id"]             = config.OAuthClientId!.Trim(),
            ["response_type"]         = "code",
            ["redirect_uri"]          = redirectUri,
            ["scope"]                 = endpoints.Scope,
            ["state"]                 = state,
            ["code_challenge"]        = challenge,
            ["code_challenge_method"] = "S256",
        };
        if (config.OAuthProveedor == OAuthProveedor.Google)
        {
            q["access_type"] = "offline";   // necesario para recibir refresh token
            q["prompt"]      = "consent";
        }
        else if (config.OAuthProveedor is OAuthProveedor.Microsoft or OAuthProveedor.OutlookPersonal)
        {
            q["prompt"] = "select_account";
        }

        var qs = string.Join("&", q.Where(kv => kv.Value is not null)
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));
        var sep = endpoints.AuthorizeEndpoint.Contains('?') ? "&" : "?";
        return $"{endpoints.AuthorizeEndpoint}{sep}{qs}";
    }

    private static async Task ResponderNavegadorAsync(HttpListenerContext context)
    {
        const string html =
            "<html><head><meta charset='utf-8'></head>" +
            "<body style='font-family:sans-serif;text-align:center;padding-top:3rem'>" +
            "<h2>Listo</h2><p>Ya podés cerrar esta pestaña y volver a la aplicación.</p></body></html>";
        var buffer = Encoding.UTF8.GetBytes(html);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer);
        context.Response.OutputStream.Close();
    }

    /// <summary>Reserva efímera de un puerto TCP libre en loopback para el redirect del navegador.</summary>
    private static int PuertoLibre()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var puerto = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return puerto;
    }
}
