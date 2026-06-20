# Integraciones externas — base de conocimiento reutilizable

Esta carpeta documenta cómo PuertoBB integra **servicios externos** (AFIP/ARCA y correo SMTP/OAuth2) de forma
que el patrón se pueda **reutilizar en futuras apps**. No repite lo que el código ya documenta; explica las
decisiones y el "cómo reusarlo".

- [`afip.md`](afip.md) — facturación electrónica AFIP/ARCA (WSAA + WSFE + padrón) vía la librería neutra `Afip.Net`.
- [`correo.md`](correo.md) — envío de comprobantes por SMTP con MailKit, autenticación Básica y **OAuth2** (Microsoft/Google/personalizado), multi-cuenta.

## Filosofía de integración (aplica a toda integración externa)

1. **Patrón adaptador sobre una interfaz del dominio.** El dominio depende de una interfaz propia
   (`IAfipService`, `IMailService`, `IMailTokenProvider`), no del SDK/librería externa. El adaptador vive en
   `PuertoBB.Services/<Area>` y traduce modelos del dominio ↔ modelos del SDK. Así el SDK se puede cambiar sin
   tocar la lógica de negocio.

2. **Librería externa neutra y reusable.** Lo verdaderamente reutilizable (el cliente de AFIP) vive en un
   proyecto **sin dependencias de la app** (`Afip.Net`, namespace `Afip`). Otra app lo referencia y listo.

3. **`ServiceResult<T>`, no excepciones, para el flujo esperable.** Las operaciones devuelven
   `ServiceResult<T>` con `Success` / `Data` / `ErrorMessage`. Las excepciones quedan para lo inesperado
   (y se traducen a `ServiceResult.Fail` con un mensaje accionable). La UI siempre chequea `.Success`.

4. **Fake vs real por DI según dos flags independientes.** La misma interfaz tiene una implementación real y una
   *fake*; la elección se hace en el arranque (`App.xaml.cs`) según `appsettings.json`. Cada integración tiene su
   propio flag bool (`MailMockService`, `AfipMockService`, ambos default `false` = real), así se puede mockear una
   sin la otra. Esto permite desarrollar, testear y hacer demos sin credenciales.
   ```csharp
   if (AfipMockService) services.AddPuertoBBAfipMock();
   else                 services.AddPuertoBBAfip(ticketCacheDir: ...);
   services.AddPuertoBBMail(usarFake: MailMockService);
   ```

5. **Errores accionables.** Los errores del servicio externo se mapean a mensajes que le dicen al usuario
   **qué hacer** (ver `AfipErrores`, `MailErrores`), no el código crudo del proveedor.

6. **Nunca loguear credenciales.** Tokens, refresh tokens, client secrets, contraseñas SMTP y el contenido del
   certificado **no** se loguean. Se loguea el resultado (OK/falló), el proveedor y el destinatario, nada más.

7. **Secretos en reposo (decisión D-24).** En esta app, los secretos se guardan en texto plano en la base SQLite
   local (app unipersonal de escritorio; el modelo de amenaza es acceso local al disco). Para una app con otro
   modelo de amenaza, cifrar en reposo (p. ej. DPAPI por-usuario en Windows) antes de persistir.

8. **Idempotencia y reanudabilidad ("Pendiente-first").** Las operaciones multi-fase (obtener CAE → PDF → mail)
   persisten primero un estado *Pendiente* y avanzan idempotentemente: reintentar nunca duplica (ver la
   recuperación anti-duplicado de CAE en `afip.md`). Un fallo de mail no revierte una emisión fiscal.
