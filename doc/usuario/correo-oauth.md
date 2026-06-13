# Correo por OAuth2 (Microsoft 365 / Gmail)

Microsoft **deshabilitó la autenticación básica** (usuario + contraseña) para SMTP en
Outlook / Microsoft 365. Si al **Probar conexión** aparece:

```
535 5.7.139 Authentication unsuccessful, basic authentication is disabled.
```

…hay que enviar el correo con **OAuth2** en lugar de contraseña.

La app lo configura en **Configuración → Correo → Autenticación: OAuth2**. Hay dos flujos,
seleccionables según el caso:

| Flujo | Cuándo usarlo | Qué pide |
|---|---|---|
| **Interactivo** | Cualquier cuenta, incluida personal `@outlook.com`/`@hotmail.com` y Gmail. Iniciás sesión en el navegador **una vez**. | Client ID (+ Tenant en Microsoft). Botón **Iniciar sesión…**. |
| **Cliente** | Casilla **Microsoft 365 de empresa** que administrás. Sin navegador, sin re-consentimiento. | Tenant ID, Client ID, Client Secret. |

Los secretos se guardan en texto plano en la base (app unipersonal, decisión D-24).

---

## Microsoft 365 / Outlook

### 1. Registrar la aplicación en Azure (Entra ID)
1. Portal de Azure → **Microsoft Entra ID → App registrations → New registration**.
2. Nombre: p. ej. `PuertoBB Correo`. Cuentas admitidas: según tu caso (una organización, o
   "cualquier organización y cuentas personales" para casillas personales).
3. Anotá **Application (client) ID** y **Directory (tenant) ID**.

### 2a. Flujo Interactivo
1. En **Authentication → Add a platform → Mobile and desktop applications**, agregá el redirect
   **`http://localhost`** (la app usa un puerto de loopback aleatorio; Microsoft acepta cualquier
   puerto bajo `http://localhost`).
2. En **API permissions → Add a permission → Microsoft Graph / Office 365 Exchange Online →
   Delegated** agregá **`SMTP.Send`** (y `offline_access`, `openid`, `email`, que la app pide solas).
3. En la app: Proveedor **Microsoft 365**, flujo **Interactivo**, pegá el **Client ID** (y el
   **Tenant ID**, o dejalo vacío para `common`). Tocá **Iniciar sesión…**, consentí en el
   navegador y **Guardá**.

### 2b. Flujo Cliente (sin navegador)
1. En **Certificates & secrets → New client secret**, generá un secreto y copiá su **valor**.
2. En **API permissions** agregá **`SMTP.Send`** como **Application permission** y otorgá
   **admin consent**.
3. Habilitá SMTP AUTH para la casilla (PowerShell de Exchange Online):
   ```powershell
   Set-CASMailbox -Identity casilla@tudominio.com -SmtpClientAuthenticationDisabled $false
   ```
   Además, el flag de organización `Set-TransportConfig -SmtpClientAuthenticationDisabled` debe
   permitirlo.
4. En la app: Proveedor **Microsoft 365**, flujo **Cliente**, cargá **Tenant ID**, **Client ID**
   y **Client Secret**. El **Email remitente** debe ser la casilla habilitada. **Guardá** y
   **Probá conexión**.

Host SMTP sugerido: `smtp.office365.com`, puerto **587**, seguridad **Auto (STARTTLS)**.

---

## Outlook.com / Hotmail / Live (cuenta **personal**)

Las cuentas personales **no** son Microsoft 365 de empresa: usan **otro host SMTP** y **otro scope**. Por eso
hay un proveedor aparte en la app.

1. Registrá la app en Azure igual que arriba (App registration), con *Supported account types* que incluya
   **"…and personal Microsoft accounts"**, y el redirect **`http://localhost`** bajo **Mobile and desktop
   applications** (no "Web").
2. En la app: Proveedor **Outlook.com (personal)**, flujo **Interactivo**, pegá el **Client ID** (Tenant vacío =
   `common`). **Iniciar sesión…**, consentí y **Guardá**.
3. La app sugiere host **`smtp-mail.outlook.com`**, puerto **587**, seguridad **Auto**, y usa el scope
   **`https://outlook.office.com/SMTP.Send`** (las personales rechazan `outlook.office365.com`).

> Solo el flujo **Interactivo** aplica a cuentas personales (el flujo Cliente es para casillas de empresa).

---

## Gmail / Google Workspace (flujo Interactivo)

1. Google Cloud Console → **APIs & Services → Credentials → Create credentials → OAuth client ID**,
   tipo **Desktop app**. Anotá **Client ID** (y **Client Secret**, que Google entrega para apps
   de escritorio).
2. En la **OAuth consent screen**, agregá el scope `https://mail.google.com/` y, si la app está en
   modo *Testing*, agregá tu cuenta como **test user**.
3. En la app: Proveedor **Google / Gmail**, flujo **Interactivo**, pegá Client ID (+ Client Secret).
   **Iniciar sesión…**, consentí y **Guardá**.

Host SMTP sugerido: `smtp.gmail.com`, puerto **587**, seguridad **Auto (STARTTLS)**.

> Alternativa simple para Gmail: dejar **Autenticación: Básica** y usar una **contraseña de
> aplicación** (requiere verificación en 2 pasos en la cuenta). No necesita OAuth.

---

## Personalizado
Para otros proveedores OAuth2: elegí **Personalizado** y cargá a mano los endpoints **Authorize**
y **Token** y el **Scope** SMTP del proveedor.

---

## Verificación
En **Configuración → Correo**, tras guardar, **Probar conexión** debe dar *Conexión correcta*.
Para el flujo interactivo, el estado muestra **✓ Conectado como {tu-email}**. Luego, reenviá un
recibo a una casilla propia antes del primer envío masivo.

## Diagnóstico (logs)
Los fallos del login OAuth y del envío quedan en el **archivo de logs** del día:
`%LocalAppData%\PuertoBB\<App>\Logs\app-AAAAMMDD.log` (líneas `[WRN]`), con el `error`/`error_description`
que devuelve el proveedor.

**Importante:** si el navegador muestra un error de **`redirect_uri`** o **`scope`**, el proveedor **no
redirige** de vuelta a la app — ese mensaje queda solo en la pantalla del navegador y la app lo reporta como
*timeout*. Corregí el redirect (registrar `http://localhost` bajo *Mobile and desktop applications*) o el
scope/proveedor y reintentá.
