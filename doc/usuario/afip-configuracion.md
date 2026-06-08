# Configurar AFIP en la aplicación (guía para el usuario)

Esta guía explica, sin tecnicismos, cómo dejar la app lista para emitir comprobantes electrónicos
(Recibos) contra AFIP/ARCA. Se hace **una sola vez** (y se repite si vence el certificado, cada ~2 años).

Hay tres etapas:

1. **Obtener el certificado digital** en AFIP.
2. **Habilitar el servicio de Facturación Electrónica** para ese certificado.
3. **Cargar todo en la app** y probar la conexión.

> **Homologación vs Producción.** *Homologación* es el ambiente de prueba de AFIP: sirve para validar
> que todo funciona, pero los comprobantes **no tienen validez fiscal**. *Producción* es el real. Se
> recomienda configurar primero homologación, probar, y recién después pasar a producción.

---

## Etapa 1 — Obtener el certificado digital (.p12)

El certificado es como una "llave" que identifica a la empresa ante AFIP. Se arma en dos partes: una
**clave privada** (se queda en la PC) y un **pedido de certificado (CSR)** que se sube a AFIP; AFIP
devuelve el **certificado** (.crt) y, juntando ambos, se obtiene el archivo final **.p12**.

### 1.1 Generar la clave y el pedido (CSR)

Necesitás la herramienta gratuita **OpenSSL** (en Windows suele venir con Git, o se instala aparte).
Abrí una terminal y ejecutá (reemplazá el CUIT y el nombre de la empresa):

```bash
# 1) Clave privada (¡guardala bien, no se comparte!)
openssl genrsa -out clave-privada.key 2048

# 2) Pedido de certificado (CSR). Completá CN (nombre) y serialNumber (CUIT sin guiones)
openssl req -new -key clave-privada.key -subj "/C=AR/O=NOMBRE DE LA EMPRESA/CN=puertobb/serialNumber=CUIT 30111111118" -out pedido.csr
```

### 1.2 Subir el CSR a AFIP y descargar el certificado

1. Entrá a AFIP con Clave Fiscal → servicio **"Administración de Certificados Digitales"**
   (o **"WSASS - Autogestión Certificados Homologación"** si es para *homologación*).
2. Creá un **alias** (ej. `puertobb`) y subí el archivo `pedido.csr`.
3. Descargá el certificado que te devuelve AFIP (archivo `.crt` o `.pem`).

### 1.3 Opción A — Armar el archivo .p12 (recomendado si ya tenés OpenSSL)

Combiná la clave privada + el certificado en un solo archivo protegido por contraseña:

```bash
openssl pkcs12 -export -in certificado.crt -inkey clave-privada.key -out puertobb.p12
```

Te va a pedir una **contraseña**: anotala, la vas a necesitar en la app. El archivo `puertobb.p12`
es el que se carga en la aplicación con el modo **P12 (con contraseña)**.

### 1.3 Opción B — Usar el .crt + .key directamente (más simple, sin OpenSSL)

Si preferís evitar el paso anterior, podés cargar los dos archivos por separado directamente
en la app, usando el modo **CRT + KEY (sin contraseña)**:

- **Certificado**: el archivo `certificado.crt` que descargaste de AFIP.
- **Clave privada**: el archivo `clave-privada.key` que generaste vos en el paso 1.1.

No hace falta ninguna contraseña. Guardá ambos archivos en un lugar seguro (como el `.p12`,
la clave privada **no se comparte**).

---

## Etapa 2 — Habilitar el servicio de Facturación Electrónica

Tener el certificado no alcanza: hay que autorizarlo a usar el web service de facturación.

1. En AFIP, entrá a **"Administrador de Relaciones de Clave Fiscal"**.
2. **Nueva relación** → Buscar el servicio **"Facturación Electrónica" (wsfe)** y asociarlo al
   **alias del certificado** creado en la etapa 1.
3. Verificá que el **punto de venta** que vas a usar esté habilitado en AFIP como
   **"Facturación Electrónica - Monotributo / Exento - Web Services"** (servicio
   *"Administración de puntos de venta y domicilios"*).

> **Si facturás como apoderado** (caso Centro Marítimo): el certificado es del apoderado, y además hay
> que hacer una **delegación** en el Administrador de Relaciones para que el apoderado pueda facturar
> en nombre del representado. En la app, tildá "Facturar como apoderado" y cargá el CUIT del apoderado.

---

## Etapa 3 — Cargar todo en la aplicación

1. Abrí la app → **Configuración**.
2. **Emisor**: completá Razón social y **CUIT** (sin guiones).
3. **AFIP / ARCA**: en **Comprobante a emitir** elegí el tipo según el régimen de la empresa
   (por defecto **Recibo C** = `15`). La **Nota de Crédito** se completa sola según la clase
   (para clase C es `13` — Nota de Crédito C) y se muestra como dato de solo lectura.
4. **Puntos de venta**: acá cargás uno o más puntos de venta. Cada uno tiene su número, su ambiente y
   su certificado. Por ejemplo, podés tener **"Homologación"** y **"Producción"** y cambiar de uno a otro
   con un click. Para cargar uno, completá el formulario de abajo y tocá **Guardar punto de venta**:
   - **Nombre**: una etiqueta para reconocerlo (ej. "Producción").
   - **Número de punto de venta**: el habilitado en AFIP.
   - **Usar homologación**: tildado = pruebas; destildado = producción (vas a ver el aviso del ambiente).
   - **Tipo de certificado**: elegí **P12 (con contraseña)** o **CRT + KEY (sin contraseña)** según cómo armaste el certificado.
     - Modo **P12**: cargá el archivo `.p12` con **Examinar…** e ingresá la **Contraseña del certificado**. Se guarda **cifrada**.
     - Modo **CRT + KEY**: cargá el `.crt` con el primer **Examinar…** y la clave privada `.key` con el segundo. No se necesita contraseña.
   - En ambos casos la app **copia los archivos a una carpeta propia**, así que después podés mover o borrar los originales sin problema.
5. En la lista, seleccioná el punto de venta que querés usar y tocá **Marcar activo** (el activo es el
   que la app usa para emitir). Para editar uno, hacé click en su fila; para borrarlo, seleccionalo y
   tocá **Eliminar**.
6. Tocá **Probar conexión**: la app prueba el **punto de venta activo** (verifica el servicio, autentica
   con su certificado y muestra el último número). Si todo está bien, vas a ver *"Conexión con AFIP correcta"*.
7. Tocá **Guardar configuración** para guardar los datos del emisor.

A partir de acá, al emitir un recibo la app pide el CAE a AFIP, genera el PDF (con CAE + QR) y lo envía.

---

## Pasar de homologación a producción

1. Repetí la **Etapa 1** pero usando el servicio de **certificados de producción** (no el de homologación).
2. Habilitá **wsfe** y el punto de venta en producción (Etapa 2).
3. En la app: cargá un **nuevo punto de venta** "Producción" (con su `.p12` de producción y **sin** tildar
   "Usar homologación"), **Marcá activo** ese punto de venta, **Probar conexión** y listo. Podés conservar
   el de homologación en la lista para volver a probar cuando quieras.

---

## Problemas frecuentes

| Mensaje / síntoma | Qué significa | Qué hacer |
|---|---|---|
| "No hay certificado configurado" | Falta cargar el `.p12` | Cargalo en Configuración (Examinar…). |
| "Autenticación: error…" al Probar conexión | El certificado no autentica | Revisá la contraseña y que **wsfe** esté habilitado para el CUIT (Etapa 2). |
| Rechazo con código **10071** | Se informó IVA en un Recibo C | No debería ocurrir (la app ya lo evita); avisá a soporte. |
| Rechazo con código **10016** | Fecha fuera de rango | Verificá la fecha/hora de la PC. |
| "El CEE ya posee un TA válido" | Se intentó autenticar de más | La app reutiliza el ticket automáticamente; reintentá en unos minutos. |
| El comprobante salió en homologación | Estaba tildado "Usar homologación" | Destildalo para producción y volvé a probar. |

> El detalle técnico de cada error queda registrado en los **logs** de la app
> (`…/PuertoBB/<App>/Logs`). Para dudas, ver `doc/arquitectura/afip-integracion.md` y `Afip.Net/README.md`.
