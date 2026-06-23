# Integración AFIP/ARCA — WSAA + WSFE

Investigación técnica para PuertoBB. Entidades exentas de IVA, emiten Recibo C (tipo 15) y Nota de Crédito C (tipo 13).

---

## Flujo general

```
[App]
  │
  ├─ 1. WSAA ──→ Ticket de Acceso (Token + Sign)   [renueva cada ~12 hs]
  │
  └─ 2. WSFE ──→ FECAESolicitar → CAE              [usa Token + Sign]
```

---

## Endpoints

### WSAA (autenticación)

| Ambiente | URL |
|---|---|
| Homologación | `https://wsaahomo.afip.gov.ar/ws/services/LoginCms` |
| Producción | `https://wsaa.afip.gov.ar/ws/services/LoginCms` |

### WSFE v1 (facturación electrónica)

| Ambiente | URL |
|---|---|
| Homologación WSDL | `https://wswhomo.afip.gov.ar/wsfev1/service.asmx?WSDL` |
| Homologación endpoint | `https://wswhomo.afip.gov.ar/wsfev1/service.asmx` |
| Producción WSDL | `https://servicios1.afip.gov.ar/wsfev1/service.asmx?WSDL` |
| Producción endpoint | `https://servicios1.afip.gov.ar/wsfev1/service.asmx` |

---

## Fase 1 — WSAA (autenticación)

### 1. Generar el TRA (Ticket de Requerimiento de Acceso)

```xml
<?xml version="1.0" encoding="UTF-8"?>
<loginTicketRequest version="1.0">
  <header>
    <source>cn=srv1,ou=facturacion,o=empresa s.a.,c=ar,serialNumber=CUIT 30XXXXXXXXX</source>
    <destination>cn=wsaa,o=afip,c=ar,serialNumber=CUIT 33693450239</destination>
    <uniqueId>4325399</uniqueId>
    <generationTime>2001-12-31T12:00:00-03:00</generationTime>
    <expirationTime>2001-12-31T12:10:00-03:00</expirationTime>
  </header>
  <service>wsfe</service>
</loginTicketRequest>
```

- `uniqueId`: entero aleatorio de 32 bits por request
- Ventana `generationTime` / `expirationTime`: 10 minutos (AFIP acepta tolerancia de ±24 hs)
- El Ticket de Acceso resultante dura **12 horas**

### 2. Firmar con CMS PKCS#7 (SHA1+RSA)

```csharp
// El certificado se guarda en la base (BLOB) y se carga desde bytes en memoria, sin tocar disco.
var cert = X509CertificateLoader.LoadPkcs12(
    options.CertificadoContenido!,           // bytes del .p12
    options.CertificadoPassword,
    X509KeyStorageFlags.EphemeralKeySet);
// Modo CRT+KEY: X509Certificate2.CreateFromPem(certPem, keyPem)

byte[] traBytes = Encoding.UTF8.GetBytes(traXml);
var signedCms = new SignedCms(new ContentInfo(traBytes), detached: false);
var signer = new CmsSigner(cert) { IncludeOption = X509IncludeOption.EndCertOnly };
signedCms.ComputeSignature(signer);

string cmsFirmadoBase64 = Convert.ToBase64String(signedCms.Encode());
// → pasar este string a loginCms() del WSAA
```

`System.Security.Cryptography.Pkcs` está incluido en .NET 10 sin paquetes adicionales.

> **Nota:** Usar `MachineKeySet` en apps WPF evita problemas con el key store cuando la app corre en distintos contextos de usuario.

### 3. Caché thread-safe del Ticket de Acceso

El TA dura 12 horas. Patrón recomendado con `SemaphoreSlim` y double-check:

```csharp
public class WsaaTokenCache
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private string? _token;
    private string? _sign;
    private DateTime _expiration = DateTime.MinValue;

    public async Task<(string Token, string Sign)> GetValidTicketAsync(
        Func<Task<(string Token, string Sign, DateTime Expiration)>> renewFunc,
        CancellationToken ct = default)
    {
        // Fast path sin lock si el ticket es válido
        if (_expiration > DateTime.Now.AddMinutes(10) && _token is not null)
            return (_token, _sign!);

        await _semaphore.WaitAsync(ct);
        try
        {
            // Double-check tras adquirir el semáforo
            if (_expiration > DateTime.Now.AddMinutes(10) && _token is not null)
                return (_token, _sign!);

            var (token, sign, expiration) = await renewFunc();
            (_token, _sign, _expiration) = (token, sign, expiration);
            return (token, sign);
        }
        finally { _semaphore.Release(); }
    }
}
```

El margen de 10 minutos antes del vencimiento evita race conditions en el límite de las 12 horas.

---

## Fase 2 — WSFE (solicitar CAE)

### Campos mínimos para Recibo C (tipo 15) — entidad exenta de IVA

> **Regla crítica:** el array `Iva` **NO debe incluirse** para tipo C. Si se incluye, WSFE retorna error 10071.

#### Cabecera `FECAECabRequest`

| Campo | Valor |
|---|---|
| `CantReg` | Cantidad de comprobantes en el request (1 para emisión individual) |
| `PtoVta` | Punto de venta configurado para "Factura Electrónica - Exento en IVA - WS" |
| `CbteTipo` | `15` (Recibo C) |

> El punto de venta debe estar habilitado en AFIP como **"Exento en IVA - Web Services"** (RG3749/2015).

#### Detalle `FEDetRequest` (por comprobante)

| Campo | Tipo | Valor |
|---|---|---|
| `Concepto` | int | 2 = Servicios (cuotas sociales son servicios) |
| `DocTipo` | int | 80 = CUIT |
| `DocNro` | long | CUIT del receptor (sin guiones) |
| `CbteDesde` | long | Número del comprobante |
| `CbteHasta` | long | Igual a `CbteDesde` |
| `CbteFch` | string | Fecha `yyyyMMdd` |
| `ImpTotal` | decimal | Monto total |
| `ImpTotConc` | decimal | **0** (siempre para tipo C) |
| `ImpNeto` | decimal | **Monto total** (clase C no discrimina IVA; `ImpTotal = ImpNeto + ImpTrib`, rechazo 10048 si no) |
| `ImpOpEx` | decimal | **0** (tipo C: exento siempre 0, rechazo 10044 si no) |
| `ImpIVA` | decimal | 0 |
| `ImpTrib` | decimal | 0 |
| `MonId` | string | `"PES"` |
| `MonCotiz` | decimal | 1 |
| `FchServDesde` | string | Obligatorio si Concepto=2: inicio del período `yyyyMMdd` |
| `FchServHasta` | string | Obligatorio si Concepto=2: fin del período `yyyyMMdd` |
| `FchVtoPago` | string | Obligatorio si Concepto=2: fecha de vencimiento del pago `yyyyMMdd` |

### Para Nota de Crédito C (tipo 13)

Mismos campos. Adicionalmente, **debe completarse** el array `CbtesAsoc` con el comprobante original:

```csharp
CbtesAsoc = new[] {
    new CbteAsoc {
        Tipo  = 15,              // tipo del recibo original (CodigoAfip del recibo)
        PtoVta = puntoDeVenta,
        Nro   = numeroOriginal,
        Cuit  = cuitEmisor
    }
}
```

### Métodos WSFE usados en PuertoBB

| Método | Uso |
|---|---|
| `FECAESolicitar` | Solicitar CAE al emitir un comprobante (incluye `CondicionIVAReceptorId`, RG 5616) |
| `FECompUltimoAutorizado` | Obtener el último número emitido para un tipo/punto de venta (al iniciar, para saber desde qué número continuar) |
| `FEDummy` | Verificar que el servicio está activo (healthcheck) |
| `FECompConsultar` | Reconciliación P2-2: si se pierde la respuesta del CAE, consultar antes de fallar |
| `FEParamGetPtosVenta` | Diagnóstico ("Probar conexión"): el PV activo existe, no está bloqueado y es CAE |
| `FEParamGetTiposCbte` | Diagnóstico: el tipo de comprobante configurado existe y está vigente |
| `FEParamGetCondicionIvaReceptor` | Diagnóstico: condiciones IVA de receptor válidas por clase (RG 5616) |

---

## Decisión de implementación

### Recomendación: implementar desde WSDL con `dotnet-svcutil`

**Motivo:**
- Ninguna librería existente tiene adopción significativa en .NET 9/10
- Solo se necesitan 3 métodos (FECAESolicitar, FECompUltimoAutorizado, FEDummy + loginCms de WSAA)
- Control total: AFIP cambia sus specs periódicamente; con una librería de terceros dependemos del ciclo de mantenimiento ajeno
- La generación con `dotnet-svcutil` toma minutos

```bash
# Instalar herramienta (una sola vez)
dotnet tool install --global dotnet-svcutil

# Generar cliente WSAA (en PuertoBB.Infrastructure)
dotnet-svcutil https://wsaahomo.afip.gov.ar/ws/services/LoginCms?wsdl \
  -n "*,PuertoBB.Infrastructure.Afip.Wsaa" \
  -o Afip/Generated/WsaaClient.cs

# Generar cliente WSFE
dotnet-svcutil https://wswhomo.afip.gov.ar/wsfev1/service.asmx?WSDL \
  -n "*,PuertoBB.Infrastructure.Afip.Wsfe" \
  -o Afip/Generated/WsfeClient.cs
```

**Paquetes NuGet a agregar en `PuertoBB.Infrastructure`:**
```
System.ServiceModel.Http
System.ServiceModel.Security
```

### Alternativa: ElRoso.ARCA

```bash
dotnet add package ElRoso.ARCA
```

- Única librería activamente mantenida a 2026 (v2.1.2, mayo 2026)
- Target .NET 9 → referenciable desde .NET 10
- Abstrae WSAA (con caché DPAPI en Windows), WSFE, WSFE Exterior
- **Contra:** 0 stars, 755 descargas → no battle-tested para producción
- Considerar si la implementación propia resulta muy costosa

---

## Librerías evaluadas

| Librería | NuGet | .NET 10 | Mantenimiento | Evaluación |
|---|---|---|---|---|
| **ElRoso.ARCA** | `ElRoso.ARCA` | ✅ (target .NET 9) | Activo (mayo 2026) | Mejor opción si se usa librería; bajo adoption |
| AfipWsfeClient | `AfipWsfeClient` | ✅ (.NET Standard 2.0) | Sin mantenimiento desde 2019 | Funcional pero desactualizado |
| BizcachaAFIP | `BizcachaAFIP` | ❌ (.NET Framework 4.7.2) | Activo | Incompatible |
| Afip.Dotnet.DI | `Afip.Dotnet.DependencyInjection` | ✅ (.NET Standard 2.0) | Prerelease 2025 | Muy temprano, riesgoso |

---

## Estructura de carpetas en Infrastructure

```
PuertoBB.Infrastructure/
└── Afip/
    ├── Generated/          ← código auto-generado por dotnet-svcutil (no editar)
    │   ├── WsaaClient.cs
    │   └── WsfeClient.cs
    ├── AfipAuthService.cs  ← implementa IAfipService.ObtenerTicketAsync
    ├── AfipService.cs      ← implementa IAfipService.ObtenerCAEAsync
    ├── WsaaTokenCache.cs   ← caché thread-safe del Ticket de Acceso
    └── Models/
        ├── CaeResult.cs
        └── ComprobanteAfipRequest.cs
```

---

## Links a documentación oficial

| Recurso | URL |
|---|---|
| Portal WS ARCA | https://www.afip.gob.ar/ws/ |
| Documentación WSFE | https://www.afip.gob.ar/ws/documentacion/ws-factura-electronica.asp |
| Manual WSAA | https://www.afip.gob.ar/ws/WSAA/WSAAmanualDev.pdf |
| Especificación WSAA 1.2.2 | https://www.afip.gob.ar/ws/WSAA/Especificacion_Tecnica_WSAA_1.2.2.pdf |
| Manual desarrollador WSFE v4.0 | https://www.afip.gob.ar/fe/documentos/manual-desarrollador-ARCA-COMPG-v4-0.pdf |

---

## Estado de implementación (2026-06-06) — ✅ extraído a librería reutilizable `Afip.Net`

Todo el cliente AFIP se movió de `PuertoBB.Services/Afip/` a un **proyecto independiente y neutro
`Afip.Net`** (namespace raíz `Afip`, sin dependencias de PuertoBB), reutilizable en otras soluciones.
Ver decisión **D-14** en `doc/decisiones/registro-decisiones.md` y la guía de desarrollador en
`Afip.Net/README.md`. Para el usuario final: `doc/usuario/afip-configuracion.md`.

```
Afip.Net/                         ← librería neutra reutilizable (net10.0, namespace Afip)
├── AfipOptions.cs                ← CUIT + cert (contenido en bytes o ruta) + UsarHomologacion (por llamada)
├── Wsaa/                         ← autenticación, COMPARTIDA por todos los web services
│   ├── ITicketProvider / TicketProvider   ← TRA → firma → loginCms → cache (por servicio)
│   ├── TraBuilder.cs             ← TRA + firma CMS PKCS#7 (uniqueId monótono)
│   ├── TicketCache.cs            ← cache thread-safe keyed por (CUIT, servicio, huella cert+ambiente)
│   ├── ITicketStore / InMemoryTicketStore / FileTicketStore (JSON en disco; barre vencidos)
│   └── Soap/ (WsaaReference.cs, WsaaSoapClient.cs, IWsaaClient.cs)
├── Wsfe/                         ← facturación electrónica (recibos)
│   ├── IWsfeService / WsfeService          ← fachada de alto nivel (usa servicio "wsfe")
│   ├── Models/ (AfipComprobanteRequest, AfipCaeResult, AfipComprobanteAsociado)
│   └── Soap/ (WsfeReference.cs, WsfeSoapClient.cs, IWsfeClient.cs, WsfeMapper.cs)
├── Wsrem/ (README.md)            ← punto de extensión documentado para Remito Electrónico (futuro)
└── DependencyInjection.cs        ← AddAfip()

PuertoBB.Services/Afip/           ← capa de dominio (sí depende de PuertoBB)
├── AfipService.cs                ← ADAPTADOR IAfipService → Afip.Net (IWsfeService)
├── AfipErrores.cs                ← traduce observaciones de AFIP a mensajes accionables
└── FakeAfipService.cs            ← CAE simulado para dev/testing (modo demo)
```

Mejoras de robustez incorporadas:
- **Cache del TA keyed por (CUIT, servicio)** y **persistido a disco como JSON** → sobrevive
  reinicios y evita el rechazo de AFIP por logins frecuentes. Carpeta: `…/PuertoBB/<App>/afip-ticket-cache`.
- **Certificado guardado en la base** (BLOB en `PuntoDeVenta`) y cargado en memoria por `Afip.Net`
  (`EphemeralKeySet`, sin tocar disco). La contraseña del certificado se guarda en texto plano (D-24).
- **Botón "Probar conexión"** en Configuración: login WSAA + FEDummy + FECompUltimoAutorizado, sin emitir.

Paquetes de `Afip.Net`: `System.ServiceModel.Http` + `System.ServiceModel.Primitives` (4.10.*),
`Microsoft.Extensions.DependencyInjection.Abstractions`,
`System.Security.Cryptography.Pkcs` (override de la transitiva vulnerable).

**Regenerar los clientes SOAP (p. ej. para producción):**
```bash
dotnet tool install --global dotnet-svcutil   # una vez
cd Afip.Net
dotnet-svcutil "https://wsaahomo.afip.gov.ar/ws/services/LoginCms?wsdl" -n "*,Afip.Soap.Wsaa" -o Soap/Generated/WsaaReference.cs
dotnet-svcutil "https://wswhomo.afip.gov.ar/wsfev1/service.asmx?WSDL"   -n "*,Afip.Soap.Wsfe" -o Soap/Generated/WsfeReference.cs
```

**Selector de modo AFIP:** se configura en `appsettings.json` de cada app → `PuertoBB:Afip` (`Mock` | `Real`),
leído en `App.xaml.cs` (enum `AfipModo`):

| `AfipMockService` | Servicio registrado | Requiere cert | Llama a AFIP |
|---|---|---|---|
| `true` | `Afip.Net.Mock` (mapper/WsfeService/caché reales, clientes SOAP simulados) | No | No |
| `false` | `AfipService` → WSAA + WSFE | Sí | Sí |

**Activar AFIP real:** poner `"AfipMockService": false` en el `appsettings.json` de cada app (default).
`MailMockService` es independiente (controla solo el Mail fake); el seed + rótulo "MODO DEMO" se activan
si cualquiera de los dos mocks está en `true`.
Luego, en Configuración: cargar el `.p12`, su contraseña, el CUIT emisor y el punto de venta habilitado
como "Exento en IVA - Web Services", elegir ambiente y usar **Probar conexión**.

> Falta solo la prueba end-to-end con certificado real. El mapeo WSFE, la firma TRA y el cache por
> servicio están cubiertos por tests; las URLs homo/prod están parametrizadas.

---

## Investigación 2026-06-12 — Panorama de clientes AFIP/ARCA y gaps de `Afip.Net`

Relevamiento de qué implementan los clientes de referencia (PyAfipWs, Afip SDK) y qué cambios
normativos recientes afectan a `Afip.Net`.

> **Actualización (mismo día):** las mejoras **1, 2 y 3** de la tabla de abajo fueron
> **implementadas** — ver la sección *"Implementación 2026-06-12"* al final del documento.
> La NC parcial (4) quedó descartada por decisión de negocio.

### ⚠ CRÍTICO — RG 5616: `CondicionIVAReceptorId` obligatorio (NO lo enviamos)

- Desde el **15/04/2025** WSFE exige informar la **condición frente al IVA del receptor** en
  `FECAEDetRequest.CondicionIVAReceptorId` (manual del desarrollador v4.0+; error **10242/10246**
  si falta o es inválido). Fue "dato no excluyente" durante la transición; con la **v4.5 del manual,
  desde el 01/09/2026 el comprobante se rechaza** sin ese campo. **Homologación ya lo valida.**
- `WsfeMapper.ToFECAERequest` **no setea el campo** → el `int` serializa `0` (inválido). La emisión
  real va a fallar con 10242 hasta que se implemente.
- El campo ya existe en `WsfeReference.cs` (generado actualizado); no hace falta regenerar el cliente.
- Valores típicos para receptores con CUIT: `1` IVA Responsable Inscripto, `4` IVA Sujeto Exento,
  `6` Responsable Monotributo, `13` Monotributista Social, `15` IVA No Alcanzado. La tabla vigente
  por clase de comprobante se consulta con `FEParamGetCondicionIvaReceptor` (ya disponible en el
  código generado, sin envolver).
- **Implementación sugerida:** campo `CondicionIvaReceptorId` en `AfipComprobanteRequest`/`WsfeCaeRequest`
  → mapper → dato por receptor (Cliente, con default configurable; el snapshot
  `Recibo.ReceptorCondicionIva` ya existe como texto para el PDF, falta el **código numérico**).

### v4.4 (vigencia 01/08/2026) — no nos afecta

`CbteFchHsGen` obligatorio y validaciones 15016/15017 aplican solo a **CAEA/contingencia** (RG 5782).
PuertoBB emite con CAE común: sin impacto.

### Qué implementan los clientes de referencia

| Funcionalidad | PyAfipWs | Afip SDK | `Afip.Net` (PuertoBB) |
|---|---|---|---|
| WSAA (TRA + CMS + cache TA) | ✅ | ✅ (gestionado) | ✅ (cache JSON por servicio) |
| WSFEv1 CAE (emisión + NC asociada) | ✅ | ✅ | ✅ |
| FECompUltimoAutorizado / FEDummy / FECompConsultar | ✅ | ✅ | ✅ (+ reconciliación P2-2) |
| Tablas de parámetros `FEParamGet*` | ✅ | ✅ | ✅ (PV, tipos, condiciones IVA — 2026-06-12) |
| CAEA (contingencia) | ✅ | ✅ | ❌ (generado, sin envolver) |
| Padrón / constancia de inscripción (`ws_sr_constancia_inscripcion`, A4/A5/A10/A13) | ✅ | ✅ | ✅ (getPersona_v2 — 2026-06-12) |
| WSCDC (constatación de comprobantes de terceros) | ✅ | — | ❌ |
| QR (Res. 4892) + PDF | ✅ | ✅ | ✅ (`Afip.Documentos`) |
| WSFEX (exportación), WSBFE (bonos), FCE MiPyME | ✅ | ✅ | ❌ (no aplican al negocio) |
| Remitos sectoriales (carne/harina/azúcar) | ✅ | — | 📋 `Wsrem/README.md` (hoja de ruta) |

### Mejoras sugeridas (priorizadas)

| # | Mejora | Motivo | Esfuerzo |
|---|---|---|---|
| 1 | **`CondicionIVAReceptorId` en la emisión** | Normativo (RG 5616); bloquea homologación hoy y producción desde 09/2026 | Medio (campo + mapper + dato por receptor + UI) |
| 2 | **`FEParamGet*` en "Probar conexión"** | Validar PV habilitado (`FEParamGetPtosVenta`), tipo de comprobante y condiciones IVA válidas antes de emitir | Bajo |
| 3 | **`ws_sr_constancia_inscripcion`** | Validar CUIT receptor contra ARCA al alta + autocompletar razón social + **derivar la condición IVA del receptor** (sinergia con #1). Requiere delegar el servicio al certificado; WSAA ya es genérico por servicio | Medio |
| 4 | NC parcial | Cambio de negocio/UI, AFIP ya lo soporta | Medio |
| 5 | CAEA / WSCDC / remitos | Sin caso de negocio actual | — |

Fuentes: [Afip SDK — error 10242](https://afipsdk.com/blog/factura-electronica-solucion-a-error-10242/),
[LLB — cambios ARCA 2026 (v4.4/v4.5)](https://llbsolutions.com/es/facturacion-electronica-argentina-cambios-arca-2026/),
[PyAfipWs — servicios soportados](https://github.com/reingart/pyafipws),
[Afip SDK — constancia de inscripción](https://docs.afipsdk.com/siguientes-pasos/web-services/padron-de-constancia-de-inscripcion).

---

## Implementación 2026-06-12 — RG 5616 + FEParamGet* + Constancia de inscripción

Las mejoras 1–3 de la investigación quedaron implementadas (159 tests verdes). Decisiones D1–D8 en
`doc/decisiones/registro-decisiones.md`.

### 1. Condición frente al IVA del receptor (RG 5616) — desbloquea la emisión real

- **Catálogo**: `PuertoBB.Core/Afip/CatalogoCondicionesIvaReceptor.cs` (códigos 1, 4, 5, 6, 7, 8, 9,
  10, 13, 15, 16) — única fuente; la UI no hardcodea textos.
- **Entidades**: `Cliente` (Empresa en CP / Agencia en CM) pasó de `CondicionIva` (string libre) a **`CondicionIvaId (int?)`**;
  el ABM usa un ComboBox del catálogo. `Recibo` suma el snapshot **`ReceptorCondicionIvaId`** (el texto
  `ReceptorCondicionIva` ahora se deriva del catálogo al emitir; el PDF no cambió).
- **Cadena de emisión**: `required int CondicionIvaReceptorId` en `ComprobanteAfipRequest` →
  `AfipComprobanteRequest` → `WsfeCaeRequest` → `WsfeMapper` setea `FECAEDetRequest.CondicionIVAReceptorId`.
  El compilador impide volver a mandar 0 por accidente.
- **Validación**: los ReciboService cortan ANTES de llamar a AFIP si el receptor no tiene condición
  (mensaje accionable por entidad; el recibo queda Pendiente/reintentable). La anulación usa
  snapshot ?? entidad ?? error legible. `AfipService` tiene guard de defensa (`<= 0`).
  `AfipErrores` traduce 10242/10243/10246.
- **Seed demo**: todas las empresas/agencias con `CondicionIvaId = 1` (verificar con "Validar en ARCA").
  Migraciones `Inicial` regeneradas (squash pre-producción).

### 2. FEParamGet* en el diagnóstico "Probar conexión"

`IWsfeClient`/`WsfeService` exponen `ObtenerPuntosVentaAsync`, `ObtenerTiposComprobanteAsync` y
`ObtenerCondicionesIvaReceptorAsync` (mapeo defensivo: `"S"/"N"`, fechas `"NULL"`, error 602 → lista
vacía). `AfipService.ProbarConexionAsync` valida tras autenticación OK — cada chequeo en su try/catch,
**nunca degrada `AutenticacionOk`**; `DiagnosticoAfip` suma `PuntoVentaOk`/`TipoComprobanteOk`
(`null` = no verificable, habitual en homologación) y la lista de condiciones IVA válidas.

### 3. Constancia de inscripción (`ws_sr_constancia_inscripcion`)

- `Afip.Net/Padron/`: `IPadronService`/`PadronService` (TA del servicio `ws_sr_constancia_inscripcion`
  — el cache por (CUIT, servicio) ya lo soportaba), `Abstractions/IPadronClient`, `Soap/PadronSoapClient`
  (homo `https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA5`, prod
  `https://aws.afip.gov.ar/sr-padron/webservices/personaServiceA5`) y `Soap/PadronMapper`.
- **Derivación de condición IVA**: monotributo→6; impuesto 30 (IVA)→1; impuesto 32 (exento)→4; sino→15.
  `errorConstancia` → resultado parcial con Observaciones; SOAP Fault "no existe persona" → null.
- **UI**: botón **"Validar en ARCA"** junto al CUIT en Empresas/Agencias — autocompleta razón social,
  domicilio y condición IVA (no pisa con vacío).
- **Requiere delegar** el servicio "Consulta a Padrón - Constancia de inscripción" al certificado en
  el Administrador de Relaciones de ARCA (además de wsfe). El padrón de homologación NO tiene los
  contribuyentes reales: "no figura" es esperable ahí.
- Mock determinista: `MockPadronClient` (CUIT 20000000001 → inexistente; 20000000002 → con errorConstancia).

**Regenerar el cliente SOAP del padrón:**
```bash
cd Afip.Net
dotnet-svcutil "https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA5?wsdl" -n "*,Afip.Soap.Padron" -o Soap/Generated/PadronReference.cs
# dotnet-svcutil lo deja en ServiceReference/Soap/Generated: moverlo a Soap/Generated y borrar ServiceReference/
```
