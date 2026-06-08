# Afip.Net

Cliente .NET **neutro y reutilizable** para los web services de AFIP/ARCA:

- **WSAA** — autenticación (firma del TRA con certificado `.p12` o par `.crt`+`.key`, `loginCms`, cacheo del Ticket de Acceso).
- **WSFE v1** — facturación electrónica (solicitar CAE, último número, healthcheck).

No tiene dependencias del dominio PuertoBB: se puede copiar/referenciar en cualquier otra solución.
Está pensado para crecer a otros servicios de AFIP (ej. Remito Electrónico) reutilizando la autenticación.

---

## Instalación / referencia

```xml
<ProjectReference Include="..\Afip.Net\Afip.Net.csproj" />
```

TFM: `net10.0`. Paquetes: `System.ServiceModel.Http/Primitives`, `Microsoft.Extensions.DependencyInjection.Abstractions`,
`System.Security.Cryptography.ProtectedData`, `System.Security.Cryptography.Pkcs`.

---

## Uso

### Registro en DI

```csharp
using Afip;
using Afip.Wsaa;

// Cache del Ticket de Acceso persistido y cifrado a disco (recomendado en escritorio):
services.AddSingleton<ITicketStore>(new FileTicketStore(@"C:\…\afip-ticket-cache"));
services.AddAfip();
// Sin la línea del FileTicketStore, el TA se cachea solo en memoria (InMemoryTicketStore).
```

### Solicitar un CAE

```csharp
using Afip;
using Afip.Wsfe;

public class MiServicio(IWsfeService wsfe)
{
    public async Task EmitirAsync()
    {
        // Modo P12 (con contraseña):
        var options = new AfipOptions
        {
            Cuit = "20111111112",                 // solo dígitos
            CertificadoRuta = @"C:\…\cert.p12",
            CertificadoPassword = "••••",
            UsarHomologacion = true               // false = producción
        };

        // Modo CRT + KEY (sin contraseña — CertificadoKeyRuta activa este modo):
        // var options = new AfipOptions
        // {
        //     Cuit = "20111111112",
        //     CertificadoRuta = @"C:\…\certificado.crt",
        //     CertificadoKeyRuta = @"C:\…\clave-privada.key",
        //     UsarHomologacion = true
        // };

        var req = new AfipComprobanteRequest
        {
            CodigoComprobante = 15,               // 15 = Recibo C
            PuntoDeVenta = 3,
            DocNroReceptor = 30711234561,
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
```

---

## Arquitectura

```
AfipOptions ─────────────── parámetros por llamada (no cacheados en DI)

Wsaa/  (autenticación COMPARTIDA por todos los servicios)
  ITicketProvider ── TicketProvider ── TraBuilder (TRA+CMS) + WSAA loginCms
                          │
                          └─ TicketCache  (keyed por CUIT+servicio)
                                 └─ ITicketStore: InMemory | FileTicketStore (DPAPI, disco)

Wsfe/  (facturación)
  IWsfeService ── WsfeService ── ITicketProvider("wsfe") + IWsfeClient ── WsfeMapper ── SOAP

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
| 10016 | Fecha del comprobante fuera de rango | Usar fecha de hoy (±tolerancia AFIP). |
| 10015 | Falta período de servicio / vto de pago | Completar `ServicioDesde/Hasta` y `VencimientoPago` (Concepto Servicios). |
| 600 / " valida" | Token/credenciales inválidos | Revisar el certificado y que el servicio `wsfe` esté habilitado para el CUIT. |
| "El CEE ya posee un TA válido" | Se pidió un TA con uno aún vigente | Reutilizar el TA cacheado (usar `FileTicketStore` para que sobreviva reinicios). |

---

## Documentación oficial

- Portal WS: https://www.afip.gob.ar/ws/
- Manual WSAA: https://www.afip.gob.ar/ws/WSAA/WSAAmanualDev.pdf
- Manual WSFE: https://www.afip.gob.ar/fe/documentos/manual-desarrollador-ARCA-COMPG-v4-0.pdf
