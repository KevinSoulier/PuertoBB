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

> **¿Lo hace un subadministrador de relaciones?** Las tres etapas en AFIP las puede realizar tanto el
> administrador de relaciones titular como un **subadministrador de relaciones** (actúan "en forma
> simultánea e indistinta"). Las pantallas y los datos resultantes son los mismos: **el certificado y el
> punto de venta quedan a nombre de la empresa**, no del subadministrador, así que el resto de la guía no
> cambia y el **CUIT emisor que se carga en la app sigue siendo el de la empresa**. Solo tené en cuenta:
>
> 1. Entrá con la **Clave Fiscal del subadministrador** y elegí **"actuar en representación de" la empresa**
>    antes de entrar a cada servicio. No uses su CUIT personal como emisor.
> 2. El subadministrador necesita tener **delegados** los servicios *"Administración de Certificados
>    Digitales"*, *"Administrador de Relaciones"* y *"Administración de puntos de venta y domicilios"*. Si
>    una pantalla le da *acceso denegado*, es porque el administrador titular todavía no se los habilitó
>    (no es un problema de la app).
> 3. **Designar otros subadministradores** queda reservado al administrador de relaciones; para todo lo de
>    facturación electrónica, el subadministrador alcanza.

---

## Etapa 1 — Obtener el certificado digital

El certificado es como una "llave" que identifica a la empresa ante AFIP. Se arma en dos partes: una
**clave privada** (se queda en la PC) y un **pedido de certificado (CSR)** que se sube a AFIP; AFIP
devuelve el **certificado** (.crt).

Hay dos formas de hacerlo. La **Opción A** (recomendada) genera todo desde la app, sin instalar nada.
La **Opción B** es para quienes ya manejan OpenSSL o ya tienen los archivos de otra app.

### Opción A (recomendada) — Generar el certificado desde la app, sin OpenSSL

La app puede crear la clave privada y el pedido (CSR) por vos. No necesitás OpenSSL ni terminal.

1. Abrí la app → **Configuración** → **AFIP / ARCA** y, en **Emisor**, asegurate de tener cargados la
   **Razón social** y el **CUIT** (se usan para armar el pedido).
2. En **Puntos de venta**, tocá **Nuevo**, completá Nombre/Número/ambiente y en **Tipo de certificado**
   elegí **Generar nuevo certificado**.
3. (Opcional) Cambiá el **Alias (CN)** —por defecto `puertobb`— y tocá **Generar CSR y clave privada**.
   El alias admite solo **letras, números, guiones (`-`) y guiones bajos (`_`)**, sin espacios ni acentos
   y hasta 64 caracteres (conviene que coincida con el alias que vas a usar en el portal de AFIP).
   La app crea la clave (queda guardada) y te ofrece **guardar el archivo `.csr`**.
4. Subí ese `.csr` a AFIP (ver 1.2 abajo) y descargá el **certificado** (`.crt`) que te devuelve.
5. Volvé a la app, abrí ese mismo punto de venta, tocá **Importar .crt…** y elegí el archivo que bajaste.
   Tocá **Guardar punto**. ¡Listo! (Mientras no importes el `.crt`, el punto figura como
   **"Pendiente .crt"** en la lista; la clave queda guardada, así que podés cerrar la app y continuar después.)

Desde ese mismo formulario podés **re-descargar** el `.csr`, el `.crt` y la clave (`.key`) cuando quieras,
o tocar **Exportar .p12…** para armar un archivo `.p12` con contraseña y **reutilizar el certificado en
otras aplicaciones**.

### Opción B — Generar con OpenSSL (alternativa)

Necesitás la herramienta gratuita **OpenSSL** (en Windows suele venir con Git, o se instala aparte).
Abrí una terminal y ejecutá (reemplazá el CUIT y el nombre de la empresa):

```bash
# 1) Clave privada (¡guardala bien, no se comparte!)
openssl genrsa -out clave-privada.key 2048

# 2) Pedido de certificado (CSR). Completá CN (nombre) y serialNumber (CUIT sin guiones)
openssl req -new -key clave-privada.key -subj "/C=AR/O=NOMBRE DE LA EMPRESA/CN=puertobb/serialNumber=CUIT 30111111118" -out pedido.csr
```

Subí el `pedido.csr` a AFIP (ver 1.2), descargá el `.crt` y después tenés dos maneras de cargarlo:

- **Modo CRT + KEY (sin contraseña)**: cargá en la app el `certificado.crt` y la `clave-privada.key`
  por separado. No hace falta contraseña.
- **Modo P12 (con contraseña)**: combiná ambos en un único archivo protegido por contraseña y cargá el `.p12`:

  ```bash
  openssl pkcs12 -export -in certificado.crt -inkey clave-privada.key -out puertobb.p12
  ```

  Te va a pedir una **contraseña**: anotala, la vas a necesitar en la app.

### 1.2 Subir el CSR a AFIP y descargar el certificado

1. Entrá a AFIP con Clave Fiscal → servicio **"Administración de Certificados Digitales"**
   (o **"WSASS - Autogestión Certificados Homologación"** si es para *homologación*).
2. Creá un **alias** (ej. `puertobb`) y subí el archivo `.csr` (el que generó la app o el de OpenSSL).
3. Descargá el certificado que te devuelve AFIP (archivo `.crt` o `.pem`).

> En todos los casos, la clave privada **no se comparte**. La app la guarda **dentro de su base** (y viaja
> en el backup), así que podés mover o borrar los archivos originales sin problema.

---

## Etapa 2 — Habilitar el servicio de Facturación Electrónica

Tener el certificado no alcanza: hay que autorizarlo a usar el web service de facturación.

1. En AFIP, entrá a **"Administrador de Relaciones de Clave Fiscal"** (como administrador o subadministrador
   de relaciones).
2. **Nueva relación** → Buscar el servicio **"Facturación Electrónica" (wsfe)** y asociarlo al
   **alias del certificado** creado en la etapa 1.
3. Repetí el paso anterior para el servicio **"Consulta a Padrón - Constancia de inscripción"**
   (`ws_sr_constancia_inscripcion`): habilita el botón **"Validar en ARCA"** del ABM de
   empresas/agencias (autocompleta razón social, domicilio y condición IVA). Es opcional pero
   recomendado.
4. Verificá que el **punto de venta** que vas a usar esté habilitado en AFIP como
   **"Facturación Electrónica - Monotributo / Exento - Web Services"** (servicio
   *"Administración de puntos de venta y domicilios"*).

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
   - **Tipo de certificado**: elegí según cómo obtuviste (u obtendrás) el certificado:
     - Modo **P12**: cargá el archivo `.p12` con **Examinar…** e ingresá la **Contraseña del certificado**.
     - Modo **CRT + KEY**: cargá el `.crt` con el primer **Examinar…** y la clave privada `.key` con el segundo. No se necesita contraseña.
     - Modo **Generar nuevo certificado**: la app crea la clave y el `.csr` (ver Etapa 1, Opción A); subís el `.csr` a AFIP e **Importás** el `.crt` que te devuelven. Podés re-descargar el `.csr`/`.crt`/`.key` o **Exportar .p12…**.
   - En ambos casos la app **guarda el contenido del certificado dentro de la base**, así que después podés mover o borrar los archivos originales sin problema, y el certificado viaja en el backup de la base. El campo muestra solo el nombre del archivo.
5. En la lista, seleccioná el punto de venta que querés usar y tocá **Marcar activo** (el activo es el
   que la app usa para emitir). Para editar uno, hacé click en su fila; para borrarlo, seleccionalo y
   tocá **Eliminar**.
6. Tocá **Probar conexión**: la app prueba el **punto de venta activo** (verifica el servicio, autentica
   con su certificado y muestra el último número). Además valida contra las tablas de AFIP que el
   punto de venta esté **habilitado para Web Services y no bloqueado**, que el **tipo de comprobante**
   configurado esté vigente, y muestra las **condiciones IVA de receptor válidas**. Si todo está bien,
   vas a ver *"Conexión con AFIP correcta"*. (En homologación es normal que AFIP no informe la lista
   de puntos de venta: el detalle lo aclara.)
7. Tocá **Guardar configuración** para guardar los datos del emisor.

> **Condición frente al IVA del receptor (obligatoria).** Desde la RG 5616, AFIP exige informar la
> condición IVA del receptor en cada comprobante (error 10242 si falta). Cada empresa/agencia debe
> tener su condición elegida en el ABM (combo "Condición frente al IVA"). El botón **"Validar en
> ARCA"** junto al CUIT consulta la constancia de inscripción y la completa automáticamente
> (requiere la delegación del paso 3 de la Etapa 2). Ojo: el padrón de **homologación** no tiene los
> contribuyentes reales, así que ahí "no figura en el padrón" es esperable; usalo en producción.

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
| El punto de venta figura **"Pendiente .crt"** | Generaste el CSR pero falta importar el `.crt` | Subí el `.csr` a AFIP, descargá el `.crt` y tocá **Importar .crt…** en ese punto de venta. |
| "Autenticación: error…" al Probar conexión | El certificado no autentica | Revisá la contraseña y que **wsfe** esté habilitado para el CUIT (Etapa 2). |
| Rechazo con código **10071** | Se informó IVA en un Recibo C | No debería ocurrir (la app ya lo evita); avisá a soporte. |
| Rechazo con código **10016** | Fecha fuera de rango | Verificá la fecha/hora de la PC. |
| Rechazo con código **10242** | Falta la condición IVA del receptor (RG 5616) | Asignala en el ABM de empresas/agencias (o usá "Validar en ARCA") y reintentá. |
| Rechazo **600** `ValidacionDeToken: No apareció CUIT en lista de relaciones` | El certificado autentica, pero ese CUIT no figura en sus relaciones para `wsfe` | Revisá que el **CUIT del Emisor** sea correcto (sin errores de tipeo) y **coincida con el del certificado**; y que `wsfe` esté autorizado a ese certificado para ese CUIT, en el **mismo ambiente** del PV (producción: Administrador de Relaciones; homologación: WSASS). |
| "Validar en ARCA" falla con error de autenticación | El servicio de padrón no está delegado | Delegá "Consulta a Padrón - Constancia de inscripción" al certificado (Etapa 2, paso 3). |
| "Acceso denegado" a Administración de Certificados / Relaciones / Puntos de venta | El subadministrador no tiene ese servicio delegado | Pedile al administrador de relaciones que se lo habilite (ver el recuadro del subadministrador y la Etapa 2). |
| "El CEE ya posee un TA válido" | Se intentó autenticar de más | La app reutiliza el ticket automáticamente; reintentá en unos minutos. |
| El comprobante salió en homologación | Estaba tildado "Usar homologación" | Destildalo para producción y volvé a probar. |

> El detalle técnico de cada error queda registrado en los **logs** de la app
> (`…/PuertoBB/<App>/Logs`). Para dudas, ver `doc/arquitectura/afip-integracion.md` y `Afip.Net/README.md`.
