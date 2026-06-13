# Afip.Net

Cliente .NET **neutro y reutilizable** para los web services de AFIP/ARCA:

- **WSAA** — autenticación (firma del TRA con certificado `.p12` o par `.crt`+`.key`, `loginCms`, cacheo del Ticket de Acceso).
- **WSFE v1** — facturación electrónica (solicitar CAE con condición IVA del receptor — RG 5616 —,
  último número, healthcheck, consulta de comprobantes y tablas de parámetros `FEParamGet*`).
- **Padrón** — constancia de inscripción (`ws_sr_constancia_inscripcion`, `getPersona_v2`): razón
  social, domicilio fiscal y condición IVA sugerida de un CUIT.

No tiene dependencias del dominio PuertoBB: se puede copiar/referenciar en cualquier otra solución.
Está pensado para crecer a otros servicios de AFIP (ej. Remito Electrónico) reutilizando la autenticación.

---

## Instalación / referencia

```xml
<ProjectReference Include="..\Afip.Net\Afip.Net.csproj" />
```

TFM: `net10.0`. Paquetes: `System.ServiceModel.Http/Primitives`, `Microsoft.Extensions.DependencyInjection.Abstractions`,
`System.Security.Cryptography.Pkcs`.

---

## Uso

### Registro en DI

```csharp
using Afip;
using Afip.Wsaa;

// Cache del Ticket de Acceso persistido a disco como JSON (recomendado en escritorio):
services.AddSingleton<ITicketStore>(new FileTicketStore(@"C:\…\afip-ticket-cache"));
services.AddAfip();
// Sin la línea del FileTicketStore, el TA se cachea solo en memoria (InMemoryTicketStore).
```

> El TA se cachea por **(CUIT, servicio, huella de credenciales + ambiente)**. Cambiar el certificado,
> la clave/contraseña o el ambiente (homologación/producción) invalida el TA cacheado y fuerza una
> autenticación real. `FileTicketStore` además barre los tickets vencidos (al arrancar y tras cada
> renovación), de modo que los archivos de un certificado anterior se eliminan solos.

### Solicitar un CAE

```csharp
using Afip;
using Afip.Wsfe;

public class MiServicio(IWsfeService wsfe)
{
    public async Task EmitirAsync()
    {
        // Modo P12 (con contraseña). Se puede pasar el contenido en memoria
        // (CertificadoContenido, prioritario) o la ruta al archivo (CertificadoRuta):
        var options = new AfipOptions
        {
            Cuit = "20111111112",                 // solo dígitos
            CertificadoContenido = p12Bytes,      // byte[] del .p12 (o usar CertificadoRuta = @"C:\…\cert.p12")
            CertificadoPassword = "••••",
            UsarHomologacion = true               // false = producción
        };

        // Modo CRT + KEY (sin contraseña — la presencia de la clave activa este modo):
        // var options = new AfipOptions
        // {
        //     Cuit = "20111111112",
        //     CertificadoContenido = crtBytes,       // o CertificadoRuta = @"C:\…\certificado.crt"
        //     CertificadoKeyContenido = keyBytes,    // o CertificadoKeyRuta = @"C:\…\clave-privada.key"
        //     UsarHomologacion = true
        // };

        var req = new AfipComprobanteRequest
        {
            CodigoComprobante = 15,               // 15 = Recibo C
            PuntoDeVenta = 3,
            DocNroReceptor = 30711234561,
            CondicionIvaReceptorId = 1,           // RG 5616 (obligatorio): 1=RI, 4=Exento, 6=Monotributo…
            ImporteTotal = 12345.67m,
            FechaComprobante = DateTime.Today,
            ServicioDesde = 20260601,             // yyyyMMdd
            ServicioHasta = 20260630,
            VencimientoPago = DateTime.Today.AddDays(30)
            // Concepto (2=Servicios) y DocTipoReceptor (80=CUIT) tienen default
        };

        AfipCaeResult r = await wsfe.SolicitarCaeAsync(options, req);
        if (r.Aprobado)
            Console.WriteLine($"CAE {r.Cae} N° {r.Numero} vto {r.FechaVencimientoCae:d}");
        else
            Console.WriteLine($"Rechazado: {r.Observaciones}");
    }
}
```

> **AFIP no devuelve PDF.** El número se resuelve internamente (`FECompUltimoAutorizado + 1`). El PDF
> del comprobante (con CAE + QR obligatorio) lo genera el consumidor.

### Diagnóstico / healthcheck

```csharp
bool servicioOk = await wsfe.VerificarServicioAsync(options);                 // FEDummy (sin certificado)
long ultimo     = await wsfe.UltimoComprobanteAsync(options, ptoVta: 3, 11);  // fuerza login + valida cert

// Tablas de parámetros (requieren ticket):
var pvs    = await wsfe.ObtenerPuntosVentaAsync(options);                  // FEParamGetPtosVenta
var tipos  = await wsfe.ObtenerTiposComprobanteAsync(options);             // FEParamGetTiposCbte
var condIva = await wsfe.ObtenerCondicionesIvaReceptorAsync(options, "C"); // RG 5616 (null = todas las clases)
```

### Generar certificado in-app (sin OpenSSL)

`CsrGenerator` arma la clave privada y el CSR a subir a AFIP, y luego empaqueta el `.p12`. Así no
hace falta OpenSSL para obtener un certificado nuevo.

```csharp
using Afip.Wsaa;

// 1) Generar clave RSA 2048 + CSR (subject: C=AR, O=razónSocial, CN=alias, serialNumber=CUIT <cuit>)
var res = CsrGenerator.Generar(cuit: "20111111112", razonSocial: "MI EMPRESA", alias: "puertobb");
File.WriteAllBytes("puertobb.csr", res.CsrPem);            // subir este .csr a AFIP
var clavePem = res.ClavePrivadaPem;                         // conservar la clave (.key PKCS#8)

// 2) AFIP devuelve el .crt → usar en modo CRT+KEY:
var options = new AfipOptions
{
    Cuit = "20111111112",
    CertificadoContenido    = File.ReadAllBytes("puertobb.crt"),
    CertificadoKeyContenido = clavePem,
    UsarHomologacion = true
};

// 3) (Opcional) Empaquetar un .p12 con contraseña para reusar en otras apps:
var p12 = CsrGenerator.ArmarP12(File.ReadAllBytes("puertobb.crt"), clavePem, password: "••••");
File.WriteAllBytes("puertobb.p12", p12);
```

### Constancia de inscripción (padrón)

```csharp
using Afip.Padron;

// Requiere delegar "Consulta a Padrón - Constancia de inscripción" al certificado en ARCA.
public class MiAlta(IPadronService padron)
{
    public async Task ValidarAsync(AfipOptions options)
    {
        var p = await padron.ConsultarPersonaAsync(options, 30711234561);
        // null = no figura en el padrón. Si existe: RazonSocial, Domicilio,
        // CondicionIvaSugeridaId (monotributo→6, IVA→1, exento→4, sino→15) y Observaciones.
    }
}
```

---

## Arquitectura

```
AfipOptions ─────────────── parámetros por llamada (no cacheados en DI)

Wsaa/  (autenticación COMPARTIDA por todos los servicios)
  ITicketProvider ── TicketProvider ── TraBuilder (TRA+CMS) + WSAA loginCms
                          │
                          └─ TicketCache  (keyed por CUIT+servicio)
                                 └─ ITicketStore: InMemory | FileTicketStore (JSON en disco)

Wsfe/  (facturación)
  IWsfeService ── WsfeService ── ITicketProvider("wsfe") + IWsfeClient ── WsfeMapper ── SOAP

Padron/  (constancia de inscripción)
  IPadronService ── PadronService ── ITicketProvider("ws_sr_constancia_inscripcion") + IPadronClient ── PadronMapper ── SOAP

Wsrem/ (futuro: Remito Electrónico — ver Wsrem/README.md)
```

Claves de diseño:
- **Una sola autenticación para todos los servicios**: `ITicketProvider.GetTicketAsync(servicio, options)`.
  Sumar un web service nuevo = pedir el TA con su nombre de servicio; el resto se reutiliza.
- **Sin estado de configuración en DI**: `AfipOptions` viaja por llamada → la config puede cambiar en runtime.
- **Tipo C (exento de IVA)**: `WsfeMapper` NO envía el array `Iva` (si se incluye, AFIP da error 10071).

---

## Regenerar los clientes SOAP (svcutil)

Si AFIP cambia el contrato o se quiere apuntar a producción:

```bash
dotnet tool install --global dotnet-svcutil    # una sola vez
cd Afip.Net
dotnet-svcutil "https://wsaahomo.afip.gov.ar/ws/services/LoginCms?wsdl" -n "*,Afip.Soap.Wsaa" -o Soap/Generated/WsaaReference.cs
dotnet-svcutil "https://wswhomo.afip.gov.ar/wsfev1/service.asmx?WSDL"   -n "*,Afip.Soap.Wsfe" -o Soap/Generated/WsfeReference.cs
dotnet-svcutil "https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA5?wsdl" -n "*,Afip.Soap.Padron" -o Soap/Generated/PadronReference.cs
# svcutil deja la salida en ServiceReference/Soap/Generated: mover el .cs a Soap/Generated y borrar ServiceReference/
```

Los archivos en `Soap/Generated/` son autogenerados: **no editar a mano**.

---

## Agregar un nuevo web service (ej. Remito Electrónico)

Ver `Wsrem/README.md`. En resumen: generar el cliente SOAP, crear `IWsXxxService`/`WsXxxService`
reutilizando `ITicketProvider.GetTicketAsync("wsXxx", …)`, y registrarlo en `AddAfip()`.

---

## Errores frecuentes de AFIP

| Código | Significado | Acción |
|---|---|---|
| 10071 | No corresponde informar IVA (tipo C) | No enviar el array `Iva` (ya contemplado en `WsfeMapper`). |
| 10242 / 10246 | Falta o es inválida la condición IVA del receptor (RG 5616) | Enviar `CondicionIvaReceptorId` válido (tabla `FEParamGetCondicionIvaReceptor`). |
| 10016 | Fecha del comprobante fuera de rango | Usar fecha de hoy (±tolerancia AFIP). |
| 10015 | Falta período de servicio / vto de pago | Completar `ServicioDesde/Hasta` y `VencimientoPago` (Concepto Servicios). |
| 600 / " valida" | Token/credenciales inválidos | Revisar el certificado y que el servicio `wsfe` esté habilitado para el CUIT. |
| "El CEE ya posee un TA válido" | Se pidió un TA con uno aún vigente | Reutilizar el TA cacheado (usar `FileTicketStore` para que sobreviva reinicios). |

---

## Documentación oficial

- Portal WS: https://www.afip.gob.ar/ws/
- Manual WSAA: https://www.afip.gob.ar/ws/WSAA/WSAAmanualDev.pdf
- Manual WSFE: https://www.afip.gob.ar/fe/documentos/manual-desarrollador-ARCA-COMPG-v4-0.pdf
