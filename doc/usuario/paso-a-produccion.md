# Paso a producción

Guía para pasar las aplicaciones (Cámara Portuaria y Centro Marítimo) del modo demo al modo
real. Complementa [afip-configuracion.md](afip-configuracion.md) (certificado y AFIP) y
[base-de-datos.md](base-de-datos.md) (dónde viven los datos y cómo hacer backup).

---

## 1. El ejecutable entregado ya es producción

La aplicación que se distribuye es un **único `.exe`** (sin archivos de configuración al lado).
Arranca directamente en modo producción: **AFIP real**, **mails reales por SMTP** y **base
vacía** (no se siembran datos de ejemplo). No hay ningún flag que tocar.

**Cómo verificar que NO está en modo demo:** el título de la ventana muestra solo el nombre y
la versión (ej. `· v1.0.0`). El rótulo **"— MODO DEMO"** solo aparece en los builds de
desarrollo (cuando algún mock está activo), nunca en el `.exe` entregado. Además, en
Configuración → AFIP/ARCA el punto de venta activo indica el ambiente (homologación o
producción).

## 2. Primera corrida en producción (base vacía)

En producción la base de datos nace vacía (no se siembra nada). Configurar en este
orden, todo desde la página **Configuración**:

1. **Datos del emisor** (pestaña AFIP/ARCA, primera sección): razón social, CUIT,
   Ingresos Brutos e Inicio de Actividades. Estos datos salen impresos en cada comprobante.
2. **Punto de venta + certificado**: cargar el punto de venta habilitado en AFIP con su
   certificado (`.p12` o CRT+KEY) y marcarlo **Activo**. El detalle completo del trámite
   (generar el certificado, autorizar el servicio `wsfe`, dar de alta el PV) está en
   [afip-configuracion.md](afip-configuracion.md).
3. **Probar conexión**: el botón valida servicio + autenticación y muestra el último
   comprobante emitido. No emitir nada hasta que esta prueba dé OK.
4. **Correo**: servidor SMTP, puerto, seguridad, remitente y **autenticación**:
   - **Básica** (usuario + contraseña): cubre Gmail con **contraseña de aplicación**,
     servicios como Brevo/SendGrid/SES (API key como contraseña), Yahoo, Zoho y SMTP propios.
   - **OAuth2**: obligatorio para **Microsoft 365 / Outlook**, que deshabilitó la auth básica
     (error `535 5.7.139`). Soporta flujo *Interactivo* (iniciar sesión en el navegador, una vez)
     o *Cliente* (Azure app, sin navegador). El paso a paso está en [correo-oauth.md](correo-oauth.md).

   Los secretos (contraseña, client secret, refresh token) se guardan en texto plano en la
   base (app unipersonal; ver decisión D-24). Probar con un reenvío a una casilla propia antes
   del primer envío masivo.
5. **Comprobante a emitir** (pestaña AFIP/ARCA): verificar el tipo (por defecto Recibo C,
   código 15; la nota de crédito asociada se deriva sola).
6. Cargar las **entidades** (Empresas o Agencias, con sus emails), los **Grupos de
   facturación** con sus ítems, y en Centro Marítimo los **Barcos** — o restaurar un backup
   si ya existe uno (Configuración → Restaurar).

**Primera emisión real:** se recomienda hacerla contra **homologación** (un punto de venta
con ambiente de homologación) y revisar el PDF resultante (CAE, QR, datos del emisor) antes
de pasar al punto de venta de producción.

## 3. Dónde viven los datos

Todo está en `%LocalAppData%\PuertoBB\<App>` (una carpeta por aplicación):

- `*.db` — la base de datos (SQLite).
- `Logs\app-AAAAMMDD.log` — un archivo de log por día (se conservan 30). Ante cualquier
  error de emisión, acá queda el detalle (incluye los rechazos de AFIP).
- `afip-ticket-cache\` — el ticket de acceso de AFIP, cifrado.

**Backup:** botón "Generar backup…" en Configuración. Recomendado: después de cada cierre de
período (CM) y al menos una vez por mes, guardando el archivo fuera de la PC (pendrive o
nube). Restaurar: Configuración → Restaurar → cerrar y reabrir la app. Detalle en
[base-de-datos.md](base-de-datos.md).

## 4. Si falla la red durante una emisión

Si AFIP no responde, el recibo queda **Pendiente** con el motivo del error y se puede
**reintentar** desde la misma pantalla: el reintento retoma el comprobante sin duplicarlo, y
si la falla fue de conexión justo después de que AFIP lo autorizó, la app **reconcilia**
automáticamente con AFIP para no emitirlo dos veces.

**Limitación conocida:** si la aplicación se **cierra** entre la falla y el reintento, esa
reconciliación automática no corre. Antes de reintentar, verificar en AFIP ("Comprobantes en
línea") si el comprobante ya salió; si salió, contactar soporte antes de volver a emitir.

## 5. Checklist final antes de operar

- [ ] El título de la ventana NO dice "MODO DEMO" (muestra solo el nombre y `· v1.0.0`).
- [ ] "Probar conexión" da OK contra el punto de venta activo.
- [ ] Mail de prueba recibido correctamente.
- [ ] Emisión de prueba en homologación revisada (PDF con CAE + QR + IIBB/Inicio de
      actividades del emisor).
- [ ] Backup inicial generado y guardado fuera de la PC.
