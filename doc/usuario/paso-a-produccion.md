# Paso a producción

Guía para pasar las aplicaciones (Cámara Portuaria y Centro Marítimo) del modo demo al modo
real. Complementa [afip-configuracion.md](afip-configuracion.md) (certificado y AFIP) y
[base-de-datos.md](base-de-datos.md) (dónde viven los datos y cómo hacer backup).

---

## 1. Qué conmuta el modo

Cada aplicación lee al arrancar el archivo **`appsettings.json`** que está en la misma carpeta
que el ejecutable:

```json
{
  "PuertoBB": {
    "ModoDemo": false,
    "Afip": "Real"
  }
}
```

| Clave | Demo | Producción |
|---|---|---|
| `ModoDemo` | `true`: siembra datos de ejemplo y los mails se simulan (no se envía nada) | `false`: base vacía, mails reales por SMTP |
| `Afip` | `"Mock"`: CAE simulado, no se contacta AFIP | `"Real"`: WSAA/WSFE reales según el punto de venta activo |

**Cómo verificar en qué modo está:** el título de la ventana. En demo termina en
**"— MODO DEMO"**; en producción muestra solo el nombre y la versión (ej. `· v1.0.0`).
Además, en Configuración → AFIP/ARCA el punto de venta activo indica el ambiente
(homologación o producción).

> ⚠️ Cambiar `appsettings.json` requiere **cerrar y volver a abrir** la aplicación.

## 2. Primera corrida en producción (base vacía)

Con `ModoDemo=false` la base de datos nace vacía (no se siembra nada). Configurar en este
orden, todo desde la página **Configuración**:

1. **Datos del emisor** (pestaña AFIP/ARCA, primera sección): razón social, CUIT,
   Ingresos Brutos e Inicio de Actividades. Estos datos salen impresos en cada comprobante.
2. **Punto de venta + certificado**: cargar el punto de venta habilitado en AFIP con su
   certificado (`.p12` o CRT+KEY) y marcarlo **Activo**. El detalle completo del trámite
   (generar el certificado, autorizar el servicio `wsfe`, dar de alta el PV) está en
   [afip-configuracion.md](afip-configuracion.md).
3. **Probar conexión**: el botón valida servicio + autenticación y muestra el último
   comprobante emitido. No emitir nada hasta que esta prueba dé OK.
4. **Correo**: servidor SMTP, puerto, seguridad, usuario, contraseña y remitente. La
   contraseña se guarda cifrada. Probar con un reenvío a una casilla propia antes del primer
   envío masivo.
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

- [ ] `appsettings.json` con `ModoDemo=false` y `Afip="Real"`.
- [ ] El título de la ventana NO dice "MODO DEMO".
- [ ] "Probar conexión" da OK contra el punto de venta activo.
- [ ] Mail de prueba recibido correctamente.
- [ ] Emisión de prueba en homologación revisada (PDF con CAE + QR + IIBB/Inicio de
      actividades del emisor).
- [ ] Backup inicial generado y guardado fuera de la PC.
