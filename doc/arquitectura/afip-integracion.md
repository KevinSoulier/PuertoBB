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
var cert = new X509Certificate2(
    rawData: File.ReadAllBytes(config.AfipCertificadoRuta!),
    password: config.AfipCertificadoPassword,
    keyStorageFlags: X509KeyStorageFlags.MachineKeySet
                   | X509KeyStorageFlags.PersistKeySet
                   | X509KeyStorageFlags.Exportable);

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

### Campos mínimos para Recibo C (tipo 11) — entidad exenta de IVA

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
| `ImpNeto` | decimal | **0** (todo es exento) |
| `ImpOpEx` | decimal | Monto total (= ImpTotal para exentos) |
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
        Tipo  = 11,              // tipo del recibo original
        PtoVta = puntoDeVenta,
        Nro   = numeroOriginal,
        Cuit  = cuitEmisor
    }
}
```

### Métodos WSFE usados en PuertoBB

| Método | Uso |
|---|---|
| `FECAESolicitar` | Solicitar CAE al emitir un comprobante |
| `FECompUltimoAutorizado` | Obtener el último número emitido para un tipo/punto de venta (al iniciar, para saber desde qué número continuar) |
| `FEDummy` | Verificar que el servicio está activo (healthcheck) |

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
├── AfipOptions.cs                ← CUIT + cert (ruta/clave) + UsarHomologacion (por llamada)
├── Wsaa/                         ← autenticación, COMPARTIDA por todos los web services
│   ├── ITicketProvider / TicketProvider   ← TRA → firma → loginCms → cache (por servicio)
│   ├── TraBuilder.cs             ← TRA + firma CMS PKCS#7 (uniqueId monótono)
│   ├── TicketCache.cs            ← cache thread-safe keyed por (CUIT, servicio)
│   ├── ITicketStore / InMemoryTicketStore / FileTicketStore (DPAPI, cifrado a disco)
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
- **Cache del TA keyed por (CUIT, servicio)** y **persistido cifrado a disco** (DPAPI) → sobrevive
  reinicios y evita el rechazo de AFIP por logins frecuentes. Carpeta: `…/PuertoBB/<App>/afip-ticket-cache`.
- **Contraseña del certificado cifrada en reposo** (DPAPI, `ISecretProtector` en `PuertoBB.Services/Security`).
- **Importación del `.p12`** a `…/PuertoBB/<App>/Certificados` al seleccionarlo (mover el original no rompe la config).
- **Botón "Probar conexión"** en Configuración: login WSAA + FEDummy + FECompUltimoAutorizado, sin emitir.

Paquetes de `Afip.Net`: `System.ServiceModel.Http` + `System.ServiceModel.Primitives` (4.10.*),
`Microsoft.Extensions.DependencyInjection.Abstractions`, `System.Security.Cryptography.ProtectedData`,
`System.Security.Cryptography.Pkcs` (override de la transitiva vulnerable).

**Regenerar los clientes SOAP (p. ej. para producción):**
```bash
dotnet tool install --global dotnet-svcutil   # una vez
cd Afip.Net
dotnet-svcutil "https://wsaahomo.afip.gov.ar/ws/services/LoginCms?wsdl" -n "*,Afip.Soap.Wsaa" -o Soap/Generated/WsaaReference.cs
dotnet-svcutil "https://wswhomo.afip.gov.ar/wsfev1/service.asmx?WSDL"   -n "*,Afip.Soap.Wsfe" -o Soap/Generated/WsfeReference.cs
```

**Selector de modo AFIP:** cada `App.xaml.cs` define `public const AfipModo Afip = AfipModo.Mock` y el enum:

| Valor | Servicio registrado | Requiere cert | Llama a AFIP |
|---|---|---|---|
| `Mock` | `Afip.Net.Mock` (mapper/WsfeService/caché reales, clientes SOAP simulados) | No | No |
| `Real` | `AfipService` → WSAA + WSFE | Sí | Sí |

**Activar AFIP real:** cambiar `Afip = AfipModo.Real` en cada `App.xaml.cs`.
Luego, en Configuración: cargar el `.p12`, su contraseña, el CUIT emisor y el punto de venta habilitado
como "Exento en IVA - Web Services", elegir ambiente y usar **Probar conexión**.

> Falta solo la prueba end-to-end con certificado real. El mapeo WSFE, la firma TRA, el cache por
> servicio y la persistencia cifrada están cubiertos por tests; las URLs homo/prod están parametrizadas.
