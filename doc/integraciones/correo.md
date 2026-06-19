# Integración de correo (SMTP + OAuth2)

Guía técnica **reutilizable** del envío de comprobantes por correo. Cubre SMTP con MailKit, los modos de
autenticación (Ninguna / Básica / **OAuth2**) y el modelo **multi-cuenta**. La guía de usuario está en
[`../usuario/correo-oauth.md`](../usuario/correo-oauth.md).

## Arquitectura

```
IMailService           → MailService (MailKit SMTP)  |  FakeMailService (ModoDemo: escribe el PDF a disco)
IMailConfigProvider    → MailConfigProvider (mapea la CuentaCorreo activa → MailConfig)
IMailTokenProvider     → OAuthTokenProvider (access token XOAUTH2; cachea y renueva)
IOAuthInteractiveFlow  → OAuthInteractiveFlow (consentimiento interactivo: auth-code + PKCE + loopback)
Modelos: PuertoBB.Core/Models/Mail/  (MailConfig, MailAutenticacion, OAuthProveedor, OAuthFlujo, OAuthPresets, ...)
```

## Modelo multi-cuenta

- `CuentaCorreo` (entidad) guarda una configuración completa de envío (SMTP + auth). Hay **varias cuentas, una
  activa** (`Configuracion.CuentaCorreoActiva => CuentasCorreo.FirstOrDefault(c => c.Activo)`), espejo del patrón
  de `PuntoDeVenta`. `MailConfigProvider` mapea la cuenta activa a `MailConfig` para el envío.
- UI master-detail en Configuración → Correo, con autocompletado de host/puerto/scope por proveedor.

## Modos de autenticación (`MailAutenticacion`)

- **Ninguna:** relays que no exigen login.
- **Básica:** usuario + contraseña (SASL LOGIN/PLAIN). Cada vez más bloqueada por los proveedores grandes.
- **OAuth2:** SASL **XOAUTH2** con un access token. Resuelve el error **`535 5.7.139`** ("basic authentication
  is disabled") de Microsoft 365 / Outlook.

`MailConfig.Validar()` valida lo mínimo según el modo y devuelve un mensaje accionable (o null si es válido).

## OAuth2 en detalle

### Proveedores (`OAuthProveedor`) y presets (`OAuthPresets`)

`OAuthPresets.Resolver(config)` resuelve endpoints y scope por proveedor; un scope manual tiene prioridad:

| Proveedor | Authorize / Token | Scope SMTP | SMTP sugerido |
|---|---|---|---|
| **Microsoft** (M365 empresa) | `login.microsoftonline.com/{tenant|common}/oauth2/v2.0/*` | `https://outlook.office.com/SMTP.Send` (+ `offline_access`) | `smtp.office365.com:587` |
| **Outlook personal** | idem Microsoft (`/common`) | `https://outlook.office.com/SMTP.Send` | `smtp-mail.outlook.com:587` |
| **Google** | `accounts.google.com/o/oauth2/v2/auth` · `oauth2.googleapis.com/token` | `https://mail.google.com/` | `smtp.gmail.com:587` |
| **Personalizado** | los que se configuren | el que se configure | el que se configure |

### Flujos (`OAuthFlujo`)

- **Interactivo (Authorization Code + PKCE)** — `OAuthInteractiveFlow`:
  1. Genera PKCE (`code_verifier` + `code_challenge` S256) y un `state` aleatorio.
  2. Abre el navegador del sistema al endpoint de autorización (Google: `access_type=offline` + `prompt=consent`
     para recibir refresh token; Microsoft: `prompt=select_account`).
  3. Escucha el callback en un **`HttpListener` en `http://localhost:<puerto-efímero>/`** (loopback).
  4. Valida `state`, canjea el `code` por **access + refresh token**, y extrae el email del `id_token`
     (solo para mostrarlo; no se valida la firma porque viene del token endpoint por HTTPS).
  5. Persiste el **refresh token** en la cuenta. Botón "Iniciar sesión…" en la UI.
- **Cliente (Client Credentials)** — sin interacción del usuario; requiere `client_secret` (y `TenantId` en
  Microsoft). Apto para casillas de servicio "send as".

### Access token (XOAUTH2)

`OAuthTokenProvider` obtiene el access token (`grant_type=refresh_token` en interactivo, `client_credentials` en
cliente), lo **cachea en memoria** y lo renueva 60 s antes de expirar. La clave de cache usa un **hash SHA-256
estable** del secreto (no `GetHashCode`, que es inestable entre procesos). `MailService.AutenticarAsync` autentica
el `SmtpClient` con `SaslMechanismOAuth2(usuario, accessToken)`.

## Registro de la app en el proveedor (para futuras implementaciones)

- **Azure (Microsoft 365 / Outlook):** registrar una App en Entra ID → permisos de **SMTP.Send** (delegado para
  interactivo) → agregar el **redirect URI** de loopback (`http://localhost`) como cliente público/nativo → tomar
  el **Client ID** (y el **Tenant ID** + un **secret** para el flujo cliente). El admin del tenant puede tener que
  habilitar SMTP AUTH para la casilla.
- **Google (Gmail / Workspace):** crear un proyecto en Google Cloud → OAuth consent screen → credencial **OAuth
  client ID** tipo *Desktop app* → habilitar el scope `https://mail.google.com/`. El refresh token requiere
  `access_type=offline` + `prompt=consent` (ya lo hace el flujo interactivo).

## Validación y robustez

- El remitente y cada destinatario se validan con `MailboxAddress.TryParse` **antes** de conectar; los
  destinatarios inválidos se descartan y se sigue con los válidos.
- `SmtpSeguridad`: Auto (StartTLS cuando esté disponible) / SslOnConnect / None.
- `MailErrores.Describir` mapea errores SMTP conocidos a mensajes accionables (el caso estrella es el `535 5.7.139`
  → "cambiá la autenticación a OAuth2") y distingue timeout de cancelación del usuario.
- **Un fallo de mail nunca revierte una emisión fiscal.** El resultado del envío (incluido el de la nota de
  crédito) se persiste en `FechaEnvioMail`/`UltimoErrorMail` para que quede trazado tras reiniciar.

## Troubleshooting

| Síntoma | Causa | Acción |
|---|---|---|
| `535 5.7.139` | El proveedor deshabilitó auth básica | Configurar **OAuth2** |
| OAuth interactivo no vuelve | El navegador no pudo redirigir al loopback | Reintentar "Iniciar sesión…"; verificar el redirect URI registrado |
| "no devolvió refresh token" | Falta consentimiento offline | Google: `prompt=consent`; revisar scopes |
| Token cliente rechazado | Falta `client_secret` o `TenantId` (Microsoft) | Completar credenciales del flujo cliente |
