# Integración AFIP / ARCA (facturación electrónica)

Guía técnica **reutilizable** de la integración con los web services de AFIP/ARCA. La guía operativa para el
usuario final (obtener y cargar el certificado) está en [`../usuario/afip-configuracion.md`](../usuario/afip-configuracion.md).

## Arquitectura

```
PuertoBB.Services/Afip/AfipService.cs   ← adaptador: IAfipService → Afip.Net (traduce modelos del dominio)
        │
        ▼
Afip.Net  (librería neutra, namespace `Afip`, SIN dependencias de PuertoBB)
   ├─ Wsaa/   WSAA: autenticación. TraBuilder (firma CMS), TicketProvider, TicketCache, FileTicketStore
   ├─ Wsfe/   WSFE: solicitud de CAE, FEParamGet*, reconciliación anti-duplicado
   ├─ Padron/ constancia de inscripción (autocompletar razón social/condición IVA por CUIT)
   └─ Soap/   clientes SOAP generados + WsfeMapper (dominio ↔ XML)
Afip.Net.Mock  ← implementación fake para AfipMockService / tests
```

- **Multi-servicio:** la librería está pensada para sumar otros servicios de AFIP (p. ej. Remitos) sin reescribir WSAA.
- **Cómo reusar en otra app:** referenciar `Afip.Net`, registrar con `services.AddAfip()` (o `AddAfipMock()`),
  implementar un `IAfipConfigProvider` que entregue CUIT + certificado + ambiente, y escribir un adaptador
  delgado a la interfaz del dominio de esa app. `Afip.Net` no conoce a PuertoBB.

## WSAA — autenticación (ticket de acceso)

1. Se arma un **TRA** (Ticket de Requerimiento de Acceso) XML con ventana de validez ±10 min y se firma como
   **CMS PKCS#7 (SHA1+RSA)** con el certificado (`TraBuilder.FirmarCms`).
2. Se llama a `loginCms()` del WSAA → devuelve un **ticket** (token + sign) válido **~12 h** por servicio.
3. El ticket se **cachea**: en memoria (`TicketCache`, con margen de 10 min antes de expirar) y, opcionalmente,
   en disco (`FileTicketStore`, JSON, con barrido automático de vencidos). La renovación es **thread-safe**
   (`SemaphoreSlim`, double-check). Si el ticket expira a mitad de un cierre masivo, se renueva solo.
4. En producción, registrar el `ITicketStore` persistido para no re-loguear en cada arranque
   (`AddPuertoBBAfip(ticketCacheDir: ...)`).

## Certificado

- Formatos soportados: **`.p12`** (con contraseña) o **CRT+KEY** (PEM). `TraBuilder.CargarCertificado(options)`
  resuelve cuál según haya o no clave privada separada.
- **Se guarda en la base** (BLOB en `PuntoDeVenta`: `CertificadoContenido` / `CertificadoKeyContenido` /
  `CertificadoPassword`) y se carga **en memoria**; nunca se escribe a disco temporal (decisión D-24).
- **Vigencia:** "Probar conexión" valida `NotAfter`/`NotBefore` y avisa si el certificado está **vencido o por
  vencer** (antes de que WSAA falle con el `600` genérico). Renovar el certificado AFIP típicamente cada 2 años.

## WSFE — emisión de CAE

- `SolicitarCaeAsync` envía el comprobante (tipo, punto de venta, importe, fechas, condición IVA del receptor) y
  recibe el **CAE** + su vencimiento.
- **RG 5616 (condición frente al IVA del receptor):** obligatoria. Sin ella AFIP rechaza con `10242`; el código
  corta **antes** de llamar (deja el recibo Pendiente con un mensaje accionable).
- **FEParamGet\*:** "Probar conexión" valida punto de venta habilitado (CAE), tipo de comprobante vigente y
  condiciones IVA del receptor válidas para la clase.

### Recuperación anti-duplicado (clave fiscal)

Si un intento de CAE falla *después* de que AFIP autorizó pero *antes* de persistir (crash/timeout), reintentar
**recupera** el comprobante de AFIP (`RecuperarComprobanteAsync` / `FECompConsultar`) en vez de emitir uno nuevo.
Así nunca se duplica un comprobante. El flujo es **idempotente**: si ya hay CAE, no se vuelve a pedir.

### Concepto, período de servicio y vencimiento de pago

Reglas del WSFEV1 (RG 2485 y modif.; *Manual del Desarrollador WSFEV1 – COMPG*) para los campos de fechas del comprobante:

- **Concepto** (`Concepto`): `1` = Productos, `2` = Servicios, `3` = Productos y Servicios. **PuertoBB emite siempre con Concepto `2`** (fijo en `Afip.Net/Wsfe/Models/AfipComprobanteRequest.cs`), porque ambas entidades cobran cuotas de **servicio**.
- **Período de servicio** (`FchServDesde` / `FchServHasta`): **obligatorio solo para Concepto 2 o 3** (no se envía para Concepto 1). Formato `AAAAMMDD` y `FchServHasta ≥ FchServDesde`. PuertoBB lo calcula como el **mes calendario completo** del período elegido (`PeriodoHelper.PrimerDia` / `UltimoDia` sobre `PeriodoAnio`/`PeriodoMes`): para una cuota mensual, p. ej. junio → `01/06`–`30/06`, sin importar el día del mes en que se emite.
- **Vencimiento de pago** (`FchVtoPago`): **lo fija el emisor, no AFIP**. Es una decisión **comercial** (el plazo que se le da al cliente para pagar), no fiscal; no hay fórmula oficial. La única validación de AFIP es `FchVtoPago ≥ fecha del comprobante` (`CbteFch`). Obligatorio para Concepto 2/3 (y FCE MiPyME). PuertoBB lo calcula como `FechaEmisión + DiasVencimiento`, donde **`DiasVencimiento` es configurable** en Configuración (**default de fábrica: 15 días**, valor habitual para cuotas mensuales). `DiasVencimiento = 0` ⇒ vencimiento = fecha de emisión (pago **al contado**, válido para AFIP).
- **Distractor a no confundir:** AFIP permite **emitir** comprobantes de servicios hasta ~10 días *antes* de la prestación. Eso es el plazo de **emisión** (`CbteFch` vs. el período), **no** el de **pago** (`FchVtoPago`).

> En las Notas de Crédito (anulación) PuertoBB pone `FchVtoPago = hoy` a propósito: la NC no genera una obligación de cobro con plazo.

## Mapeo de errores

`PuertoBB.Services/Afip/AfipErrores.cs` traduce los códigos de AFIP a mensajes accionables: `10071`/`10044`
(IVA en clase C), `10048` (total inconsistente), `10016`/`10015` (fechas/período), `10242`/`10243`/`10246`
(condición IVA receptor RG 5616), `600` (credenciales/certificado).

## QR del comprobante

`Afip.Documentos/Qr/AfipQrPayload.cs` arma el QR según la spec de ARCA (`https://www.afip.gob.ar/fe/qr/?p=<base64>`
con `ver, fecha, cuit, ptoVta, tipoCmp, nroCmp, importe, moneda, ctz, tipoDocRec, nroDocRec, tipoCodAut, codAut`).
El importe se serializa con **punto decimal** (no coma es-AR) y la misma fuente `(DocTipo, DocNro)` que usa WSFE,
para que QR y comprobante no diverjan.

## Homologación vs Producción

- Se selecciona con `AfipOptions.UsarHomologacion` (desde `appsettings.json: PuertoBB:Afip`, default **Mock**).
- URLs distintas por ambiente (WSAA/WSFE). El default seguro evita golpear producción por error.
- Antes de producción: probar en **homologación** con el certificado real.

## Dinero

`decimal` en todo el pipeline. El único `double` es el marshalling SOAP (`WsfeMapper`), redondeado a 2 decimales
`AwayFromZero` **antes** del cast. La suma de líneas persistidas es exactamente el total enviado a AFIP.

## Troubleshooting

| Síntoma | Causa probable | Acción |
|---|---|---|
| Error `600` al emitir | Certificado vencido, servicio `wsfe` no habilitado para el CUIT, o ambiente equivocado | "Probar conexión" muestra vigencia del certificado y estado del servicio |
| Rechazo `10242`/`10243` | Falta condición IVA del receptor (RG 5616) | Asignarla en Empresas/Agencias y reintentar |
| Comprobante "perdido" tras timeout | CAE autorizado pero no registrado | Reintentar: se recupera de AFIP sin duplicar |
| Punto de venta no habilitado | PV no es de tipo CAE o está bloqueado | Verificar en AFIP los PV habilitados para Web Services |
