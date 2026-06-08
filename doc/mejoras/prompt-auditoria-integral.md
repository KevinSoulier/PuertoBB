# Prompt — Auditoría integral de validación y homogeneización (PuertoBB)

> Pegar el bloque de abajo a un agente nuevo (cold start) parado en la raíz del repo `PuertoBB`.
> Es una auditoría **de solo lectura**: el agente NO corrige, solo **detecta, evidencia y lista** hallazgos priorizados para corregir después.

---

## PROMPT

Sos un auditor técnico senior de .NET / WPF / EF Core / integración AFIP. Tu trabajo es hacer una
**auditoría exhaustiva de validación y homogeneización** de toda la solución PuertoBB y producir un
**informe de hallazgos priorizados**. NO modifiques código ni docs. NO arregles nada. Solo detectá,
evidenciá con `archivo:línea` y recomendá la corrección.

### Contexto de la solución (verificá, no asumas)

PuertoBB es una solución .NET 10 con **dos apps WPF gemelas** que comparten capas:

- `PuertoBB.Core` — dominio puro, sin dependencias de otros proyectos (entidades, enums, interfaces, modelos, `ServiceResult`).
- `PuertoBB.Infrastructure` — EF Core + SQLite (DbContexts CP/CM, configuraciones, migraciones, repositorios). Solo depende de Core.
- `PuertoBB.Services` — lógica de negocio, AFIP (adaptador), Mail, PDF, seguridad. Solo depende de Core (+ libs Afip).
- `Afip.Net` / `Afip.Net.Mock` — cliente AFIP neutro (WSAA/WSFE) y su mock. Sin dependencias de PuertoBB.
- `Afip.Documentos` — generación de PDF/QR de comprobantes (QuestPDF/QRCoder).
- `CamaraPortuaria.UI` y `CentroMaritimo.UI` — dos apps WPF (WPF-UI Fluent), punto de composición DI.
- `PuertoBB.Tests` / `Afip.Documentos.Tests` — xUnit + NSubstitute + SQLite in-memory.

**Dato clave de dirección del trabajo:** el desarrollo reciente se hizo mayormente sobre
**CentroMaritimo (CM)**, así que CM es la referencia más actualizada. CamaraPortuaria (CP) probablemente
quedó **rezagada**: funcionalidades, correcciones, estilos, iconos y tooltips que se aplicaron en CM pueden
**no haberse portado a CP**, o haberse portado parcial/incorrectamente. Tratá a CM como el "estado deseado"
y verificá funcionalidad por funcionalidad que **todo lo compartido haya bajado a CP correctamente**. Cada
cosa presente en CM y ausente/distinta en CP (sin justificación de negocio) es un hallazgo.

Las reglas de arquitectura, convenciones de código y de diseño están documentadas en `doc/`:
- `doc/arquitectura/convenciones.md`, `doc/arquitectura/dependencias.md`, `doc/arquitectura/datos.md`,
  `doc/arquitectura/afip-integracion.md`, `doc/arquitectura/flujos.md`, `doc/arquitectura/solucion.md`
- `doc/diseño/*` (Fluent/WPF-UI, navegación, UX, paletas)
- `doc/negocio/*` (reglas de negocio CP/CM, cierre de período)
- `doc/decisiones/registro-decisiones.md` (decisiones D-1..D-16+)

**Empezá leyendo todo `doc/`** y tratalo como la fuente de verdad de lo que el código *debería* cumplir.
Cualquier divergencia entre código y doc es un hallazgo (indicá cuál de los dos está mal o desactualizado).

### Principio rector: HOMOGENEIZACIÓN

El mayor riesgo de esta solución es la **duplicación en espejo**: casi todo existe dos veces
(CamaraPortuaria ↔ CentroMaritimo, y dentro de Core/Infrastructure dominio CP ↔ CM). Por cada par de
artefactos equivalentes, comparalos lado a lado y reportá **toda divergencia no justificada**:
estructura, nombres, firmas, estilos, manejo de errores, logging, validaciones, textos de UI, estilos XAML,
comportamiento. Si dos cosas hacen lo mismo deberían hacerlo igual; si difieren, o está justificado por
negocio (documentalo) o es un bug/inconsistencia (reportalo).

### Hallazgos conocidos (confirmados por el usuario) — validá causa raíz y proponé rediseño

Estos problemas ya están confirmados. No tenés que "descubrir si existen": tenés que **encontrar la causa
raíz con evidencia (`archivo:línea`), medir su alcance (¿pasa en CP, en CM, o en ambas?) y proponer el
rediseño correcto**, integrándolo al plan de corrección y a la documentación estándar (Paso 9).

- **HK-1 — La descripción/detalle del recibo cambia según desde dónde se genera o se abre.**
  Ejemplo real: un recibo generado desde **Cierre de período** muestra el **N° de voucher** en el detalle,
  pero el **mismo recibo abierto desde Recibos** muestra otra cosa. La descripción **no se está persistiendo**:
  hoy se **reconstruye/deriva en runtime** según el contexto, así que difiere por pantalla.
  - **Requerimiento:** el detalle exacto que se emite/envía en el recibo debe **guardarse en la base** (snapshot
    inmutable del comprobante) y mostrarse **siempre igual** desde cualquier parte de la app y en el PDF/mail.
  - A auditar: dónde se arma el detalle en cada flujo (Cierre de período, Recibos, Emisión masiva, NC),
    qué se persiste hoy en la entidad `Recibo`/`NotaDeCredito` vs qué se recalcula al mostrar, y si CP y CM
    divergen en esto. Proponé el campo/tabla donde persistir el detalle y la migración correspondiente (CP+CM).

- **HK-2 — El recibo solo permite un único detalle + un total; debe permitir múltiples ítems.**
  Hoy la carga del recibo acepta **una sola línea de detalle y un total**. Debe poder cargar **varios ítems**
  (descripción + importe por ítem) cuando se quiera más de uno, con el total como suma de los ítems.
  - A auditar: modelo de datos (`Recibo` y si existe o no una entidad de ítems/líneas), el ViewModel y la Page
    de carga de recibo (en ambas apps), el servicio de emisión, la generación de PDF (`Afip.Documentos`/`PdfService`)
    y el mapeo a AFIP (WSFE) — verificá que todo el pipeline soporte N ítems, no uno solo.
  - **Requerimiento:** introducir una entidad de **ítems del recibo** (líneas) persistida, edición de N ítems en la
    UI, total derivado, y que PDF/mail/AFIP reflejen los ítems. Coordinar con HK-1 (los ítems persistidos son
    parte del snapshot inmutable del detalle).

- **HK-3 — Diseño objetivo: el `Recibo` debe ser un agregado autocontenido (snapshot).**
  La causa de fondo de HK-1 y HK-2 es que el `Recibo` **no guarda su propio detalle completo**, sino que lo
  reconstruye desde otras entidades según la pantalla. Estado actual confirmado (verificalo en el código):
  `Recibo` tiene un único `decimal Importe` + un único `string Detalle`; no existe una entidad de ítems/líneas;
  en CentroMaritimo el detalle "consolidado" se deriva en runtime desde la colección `Vouchers` (por eso muestra
  los N° de voucher desde Cierre de período y algo distinto desde Recibos).
  - **Requerimiento de diseño (canónico, a documentar en Paso 9):** el `Recibo` debe ser un **agregado
    autocontenido**: al emitirse, **copia y persiste todo lo necesario para regenerarse idéntico** sin depender
    de otras entidades vivas. Eso incluye:
    - una colección persistida de **ítems del recibo** (líneas): descripción + importe (y cantidad/precio
      unitario si aplica) por ítem; el `Importe` total del recibo es la **suma de los ítems**;
    - el **detalle/encabezado** y cualquier dato mostrado o enviado, congelado al momento de emitir (snapshot
      inmutable), de modo que abrir el recibo desde **Recibos, Cierre de período, Control de pagos, PDF o mail**
      muestre **exactamente lo mismo** siempre;
    - cuando el recibo proviene de vouchers (CM), los N° de voucher / referencias que hoy se derivan deben quedar
      **materializados como ítems o líneas del snapshot**, no recalculados.
  - Esto reemplaza el patrón actual de "un `Detalle` string + `Importe` único". Mantené la relación con `Vouchers`
    para trazabilidad, pero el **detalle mostrado/emitido sale del snapshot del recibo**, no de la consulta a vouchers.
  - Aplicá el mismo diseño a `NotaDeCredito` donde corresponda (su detalle también debe ser un snapshot).
  - Entregables esperados para HK-3: modelo de datos propuesto (entidad de ítems + campos de snapshot), su
    configuración EF e índices, la **migración CP+CM** con **estrategia de backfill** para recibos históricos
    (rellenar ítems/detalle a partir del mejor dato disponible: el `Detalle` actual y/o los vouchers asociados),
    y el impacto en servicio de emisión, ViewModels/Pages (edición de N ítems), PDF y mapeo AFIP (WSFE con N ítems).

- **HK-4 — Anulación / Nota de crédito: deben respetar el formato (snapshot) del recibo original.**
  Una vez que el `Recibo` es un agregado autocontenido con ítems (HK-3), la **anulación** y la **nota de crédito**
  tienen que **reflejar exactamente el mismo detalle/ítems del recibo que acreditan**. Estado actual confirmado
  (verificalo): `NotaDeCredito` **no guarda detalle ni importe ni ítems** — solo referencia `ReciboOriginalId` +
  datos del comprobante AFIP (PV, tipo, código, número, CAE, fechas). Por lo tanto el detalle de la NC hoy se
  **deriva del recibo original** y arrastra el mismo problema de inconsistencia que HK-1.
  - **Requerimiento de diseño (canónico, a documentar en Paso 9):**
    - La **NC también es un comprobante autocontenido**: debe **persistir su propio snapshot de ítems/detalle e
      importe**, copiado del recibo original al momento de emitirla (no recalculado al abrirla). Así la NC se ve y
      se emite igual desde cualquier pantalla, PDF y mail.
    - **Anulación total** = NC por el **total** del recibo, replicando **todos los ítems** del snapshot del recibo;
      al confirmarse, el recibo pasa a `Estado = Anulado`. Definí (y documentá) si existe **NC parcial**
      (acreditar solo algunos ítems / un importe menor): si aplica, la NC copia el subconjunto de ítems y el
      recibo **no** queda Anulado (o pasa a un estado/figura que haya que definir). Si no aplica, dejarlo explícito.
    - **Formato heredado:** la NC debe respetar el formato establecido por el recibo — mismo emisor/receptor,
      mismos ítems (texto e importes), misma estructura de detalle, mismo tipo de comprobante derivado
      (la NC del tipo correspondiente según `CatalogoComprobantesAfip`), y referencia al original en AFIP vía
      `CbtesAsoc` (WSFE).
    - Coherencia de estados: `Anulado` ya existe en `ReciboEstado`; verificá las **transiciones válidas**
      (no anular dos veces, no anular un recibo `Pendiente` sin CAE, qué pasa con el control de pagos de un
      recibo anulado) y que sean idénticas en CP y CM.
  - Entregables de HK-4: modelo de `NotaDeCredito` con snapshot de ítems/detalle/importe + su configuración EF y
    migración CP+CM; reglas de anulación total/parcial documentadas; impacto en servicio (emisión de NC, cambio de
    estado, idempotencia), ViewModels/Pages, PDF de la NC y mapeo AFIP. Mantener paridad CP↔CM.

Estos hallazgos deben quedar reflejados en: la tabla de hallazgos priorizada, el plan de corrección por lotes,
y la documentación de negocio + implementación técnica (estructura de datos del recibo y reglas del detalle).

### Procedimiento paso a paso (recorré TODAS las capas, sin saltarte ninguna)

**Paso 0 — Línea base.**
1. Listá proyectos y referencias (`*.csproj`). Confirmá `TargetFramework`, `Nullable`, `LangVersion`, `ImplicitUsings` coherentes entre proyectos; reportá divergencias.
2. Compilá la solución entera y reportá **todos** los warnings (objetivo histórico: 0 warnings). `dotnet build` en limpio.
3. Corré toda la suite de tests y reportá pasa/falla y tiempo. `dotnet test`.

**Paso 1 — Reglas de dependencias (doc/arquitectura/dependencias.md).**
- Verificá que Core no referencie a nadie; Services e Infrastructure solo a Core; UI es el único punto de composición.
- Buscá violaciones de capa: `DbContext`/EF usado fuera de Infrastructure; `MessageBox`, `System.Windows`, o tipos WPF en Core/Services; lógica de negocio en code-behind (`.xaml.cs` con algo más que `InitializeComponent`); ViewModels que tocan `DbContext` directo.
- Buscá referencias de proyecto innecesarias o faltantes.

**Paso 2 — Core (dominio).**
- Entidades: ¿todas heredan de `BaseEntity`? ¿`CreatedAt/UpdatedAt` se setean de forma consistente?
- Compará entidades equivalentes CP vs CM (`Recibo`, `NotaDeCredito`, `GrupoFacturacion`, `ConceptoRecibo`, `PuntoDeVenta`, `Configuracion`) y reportá divergencias de campos/tipos/nullability no justificadas.
- Enums (`ReciboEstado`, `TipoComprobante`, `TipoEntidad`): ¿usados de forma consistente? ¿valores mágicos repetidos como strings/ints en vez del enum?
- Interfaces: ¿cada repo/servicio tiene su interfaz? ¿firmas async con `CancellationToken` en todo I/O? ¿`ServiceResult<T>` como retorno de servicios de negocio?
- `CatalogoComprobantesAfip`: confirmá que es la única fuente de la tabla AFIP (Recibo C = 15, NC derivada). Buscá tablas/constantes AFIP duplicadas en otro lado.
- Buscá modelos/DTOs duplicados o casi-iguales que deberían unificarse.

**Paso 3 — Infrastructure (datos / EF).**
- DbContexts CP/CM: compará configuración, convenciones, `OnModelCreating`. ¿Aplican `IEntityTypeConfiguration` de forma consistente?
- Configuraciones EF: índices únicos, índices parciales (consolidados), `maxLength`, precisión decimal de importes, claves foráneas, `DeleteBehavior`, columnas requeridas. Reportá importes sin precisión definida, strings sin longitud, FKs sin índice.
- **Migraciones:** ¿el `ModelSnapshot` está sincronizado con el modelo actual? (ejecutá `dotnet ef migrations has-pending-model-changes` o equivalente para cada contexto). ¿Migraciones CP y CM equivalentes están alineadas? ¿hay migraciones huérfanas o pasos destructivos?
- Repositorios: compará `RepositoryBase` + repos concretos CP vs CM. Buscá queries N+1, `.Include` faltantes/sobrantes, falta de `AsNoTracking` en lecturas, `ToListAsync` que materializa de más, falta de `CancellationToken`, tracking innecesario.
- Seguridad de datos: contraseñas/certificados cifrados en reposo (DPAPI) — confirmá que no se persista nada sensible en claro.

**Paso 4 — Services (negocio + integraciones).**
- Manejo de errores según `convenciones.md`: servicios devuelven `ServiceResult<T>`, nunca propagan excepciones a la UI; `try/catch` de I/O externo (AFIP/mail/PDF) vive en Service; repos envuelven `DbUpdateException`. Reportá cada incumplimiento.
- Logging `ILogger<T>` estructurado (propiedades nombradas, no interpolación); niveles correctos (Info/Warning/Error/Critical). Buscá `Console.WriteLine`/`Debug.WriteLine`. Buscá operaciones de negocio importantes sin log.
- Compará `CamaraPortuariaReciboService` vs `CentroMaritimoReciboService`, los `PdfService`, los `*ConfigProvider`: misma estructura, mismas validaciones, mismo manejo de errores. Reportá divergencias.
- AFIP (`AfipService` adaptador + `Afip.Net`): flujo WSAA→WSFE, cache de ticket por servicio, mapeo WSFE (sin array IVA en tipo C, `CbtesAsoc` en NC, importes exentos), parseo de respuesta aprobada/rechazada, manejo de errores y observaciones AFIP. Verificá que coincida con `doc/arquitectura/afip-integracion.md`.
- Concurrencia/idempotencia: numeración de comprobantes y vouchers, doble emisión, condiciones de carrera en `FECompUltimoAutorizado` + `FECAESolicitar`.
- Mail (`MailService`/`FakeMailService`): manejo de fallo de envío (recibo queda Emitido, warning), timeouts, seguridad SMTP.
- `Formato`, `PeriodoHelper`, `EstadoReciboHelper`: lógica duplicada entre apps/capas que debería estar centralizada acá.

**Paso 5 — Librerías Afip.Net / Afip.Net.Mock / Afip.Documentos.**
- ¿`Afip.Net` es realmente neutra (sin tipos de PuertoBB)? ¿La API pública es coherente y bien encapsulada?
- Cache/almacenamiento de tickets (`TicketCache`, `FileTicketStore`, `InMemoryTicketStore`): expiración, concurrencia, cifrado en disco.
- `TraBuilder` / firma CMS: corrección de la firma PKCS#7, flags del certificado.
- `Afip.Net.Mock` debe reflejar fielmente los contratos reales (mismas firmas, comportamiento plausible). Reportá desajustes con la implementación real.
- `Afip.Documentos`: plantilla del comprobante vs requisitos AFIP/QR (`AfipQrBuilder`/`AfipQrPayload` según especificación del QR de AFIP). Datos obligatorios del PDF.

**Paso 6 — UI (las dos apps WPF) + homogeneización fina.**
- `App.xaml.cs`: registro DI completo (toda interfaz resuelta, sin servicios faltantes/duplicados, lifetimes correctos), Serilog configurado antes del host, handlers globales (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`), migración+seed al arranque. Compará CP vs CM.
- MVVM: `BaseViewModel`/`PageViewModel`/`RelayCommand`/`AsyncRelayCommand` — ¿idénticos entre apps? Si divergen sin razón, es deuda. ViewModels sin `try/catch`, sin lógica en code-behind, sin `DbContext`.
- Comandos async: `async void` solo en handlers de evento; `await` correcto; no bloquear UI (`.Result`/`.Wait()`); `IsBusy`/estado de carga; `CanExecute`.
- XAML: estilos en `Styles.xaml`/`Colors.xaml` vs valores hardcodeados; recursos compartidos; bindings rotos (propiedades inexistentes), `Mode`/`UpdateSourceTrigger` faltantes; `MessageBox` directo (prohibido, usar `IDialogService`). Compará página por página las que existen en ambas apps (Dashboard, Configuración, Recibos, Grupos, EmisionMasiva, ControlPagos, ConceptosRecibo): mismo layout, misma UX, solo cambia el color de acento.
- Textos de UI en español, consistentes (mismos rótulos para mismas acciones en ambas apps).
- Converters duplicados (`EstadoReciboToBrushConverter`, `BoolToVisibilityConverter`) entre apps: ¿deberían compartirse? Reportá divergencias de comportamiento.
- `SeedData` CP vs CM: coherencia y que no se ejecute en producción sin control (`ModoDemo`).
- Accesibilidad/UX según `doc/diseño/ux-reglas.md`.

**Paso 6.bis — Consistencia visual de componentes (iconos, tooltips, botones de operación).**
- **Botones de operación**: en TODAS las páginas con acciones (en especial **Vouchers, Cierre de período, Recibos, Emisión masiva, Control de pagos**, y también Agencias/Empresas, Barcos, Grupos, Conceptos, Configuración, Dashboard), verificá que cada botón de operación:
  - **Tenga `ToolTip`** descriptivo (ninguno sin tooltip).
  - **Tenga un icono** (`SymbolIcon`/`SymbolRegular` de WPF-UI u origen equivalente) **acorde a la operación** que realiza.
  - Use el **mismo icono para la misma operación en toda la solución** (ej.: Emitir, Anular, Enviar mail, Imprimir/PDF, Guardar, Editar, Eliminar, Agregar, Refrescar, Buscar, Exportar, Cerrar período, Pagar/registrar pago, Backup). Construí una **tabla operación → icono → tooltip** y reportá toda inconsistencia: misma operación con iconos distintos, iconos que no representan la acción, tooltips ausentes o con texto distinto para la misma acción entre páginas/apps.
- **Iconos en general (toda la solución)**: ítems del menú/navegación lateral, encabezados, estados, badges, diálogos. Verificá set de iconos homogéneo (no mezclar familias), tamaños/espaciados consistentes, y que CP y CM usen **los mismos iconos para las mismas cosas** (solo cambia el color de acento, no el icono).
- **Botones de operación entre CP y CM**: compará página por página los botones equivalentes — mismo conjunto de acciones, mismo orden, mismo icono, mismo tooltip, mismo estilo. Reportá los que existen en CM y faltan o difieren en CP.
- Estilos de botón: que salgan de `Styles.xaml` (no estilos inline ad-hoc repetidos); mismos `Appearance`/variantes para mismos roles (primario/secundario/peligro).

**Paso 6.ter — Paridad funcional CM → CP (lo compartido bajó correctamente).**
Recorré funcionalidad por funcionalidad lo que CP y CM comparten y verificá que lo trabajado en CM esté
reflejado en CP. Para cada funcionalidad compartida (emisión individual, **emisión masiva**, **recibos**,
**control de pagos**, notas de crédito, grupos de facturación, conceptos de recibo, puntos de venta,
configuración —AFIP/Mail/Backup—, dashboard, diálogos):
- ¿Existe en ambas apps? ¿La lógica del ViewModel es equivalente (validaciones, estados, manejo de errores, logging, comandos, `CanExecute`)?
- ¿La vista XAML tiene los mismos controles, columnas, filtros, acciones y textos?
- ¿Las correcciones de bugs hechas en CM están también en CP? (compará commits/diffs recientes de CM contra el código actual de CP).
- Listá en una tabla: `Funcionalidad | Estado CM | Estado CP | ¿Portado? | Diferencia`.

**Paso 7 — Tests.**
- Cobertura por capa: ¿qué servicios/flujos críticos no tienen test? (emisión, NC, cierre de período, vouchers, mapeo AFIP, backup). Listá huecos.
- Calidad: tests que no asertan nada útil, dependientes de orden, con datos compartidos, sin cubrir caminos de error (`Fail`). Paridad CP vs CM en los tests de flujos equivalentes.
- ¿Hay tests ignorados/skipped? ¿flaky?

**Paso 8 — Transversal (toda la solución).**
- Nullability: warnings de null suprimidos con `!` sin justificación; posibles `NullReferenceException`.
- Async: `async void`, falta de `ConfigureAwait` donde corresponda, `Task` no esperados, `CancellationToken` no propagado.
- `IDisposable`/recursos: streams, `HttpClient`/clientes SOAP, conexiones — uso de `using`, no fugas.
- Cultura/formato: parseo y formato de decimales/fechas con `CultureInfo` explícita (riesgo es-AR vs invariante) en importes, CAE, períodos, QR.
- Seguridad: secretos en claro (config, logs, repo), rutas hardcodeadas, `catch` que se traga excepciones, datos sensibles logueados.
- Código muerto: clases/métodos/usings sin uso, TODO/FIXME/HACK reales, comentarios obsoletos.
- Consistencia de nombres: dominio en español, técnico en inglés, un archivo por clase, nombre archivo = clase.
- Strings mágicos / números mágicos repetidos que deberían ser constantes/enums compartidos.

**Paso 9 — Estandarización: documentación + skills para agentes.**
Objetivo de este paso: lograr que cualquier agente que implemente o diseñe a futuro tenga **una sola fuente
de verdad** que leer, para que **no haga cosas redundantes ni se desvíe de los lineamientos** (hoy el usuario
tiene que aclararlo a mano cada vez). Este paso parte de los hallazgos de los pasos 0–8.

9.1 — **Auditoría de la documentación existente (`doc/`).** Detectá: huecos (patrones que el código usa pero
   nadie documenta), contradicciones doc↔doc, docs desactualizados vs el código real, y temas que están
   implícitos "en la cabeza" pero no escritos. El estándar a documentar debe reflejar **el mejor patrón ya
   presente en el código (normalmente el de CM)**, no inventar uno nuevo.

9.2 — **Propuesta de documentación canónica a sumar/actualizar.** Redactá (como borrador para aprobar) los
   documentos que faltan para que todo quede estandarizado en tres ejes:
   - **Diseño / UI**: un *design system* escrito — paleta y acentos por app, tipografía y espaciados, estilos
     de botón por rol (primario/secundario/peligro), **catálogo canónico operación → icono → tooltip**
     (el del Paso 6.bis), patrones de página (lista + filtros + acciones + diálogo), estados de carga/vacío/error,
     reglas de los diálogos. Que un agente pueda diseñar una página nueva sin preguntar.
   - **Lógica de negocio**: glosario de dominio CP/CM, reglas de emisión/NC/cierre de período/vouchers/pagos,
     estados del recibo y transiciones, numeración/idempotencia, qué es regla de negocio compartida vs específica
     de cada app. Una sola fuente por regla (sin duplicar entre `doc/negocio/*`).
   - **Implementación técnica**: recetas paso a paso de "cómo se hace acá" — *cómo agregar una entidad nueva
     end-to-end* (Core → Configuration EF → migración CP+CM → repo+interfaz → service+`ServiceResult` → DI →
     ViewModel → Page XAML → test), patrón de manejo de errores/logging, patrón async/`CancellationToken`,
     patrón de DI, convención de tests. Incluir un **checklist de "Definition of Done"** por tipo de cambio.

9.3 — **Propuesta de skills/comandos (`.claude/commands/`).** Evaluá los skills actuales
   (`arquitecto`, `desarrollador`, `diseño-wpf`, `testing`, `validar-plataforma`, `cargar-todo`, etc.) y proponé
   crear o actualizar los que hagan falta para que el agente cargue el estándar correcto rápido y no diverja.
   Candidatos a evaluar (proponé solo los que aporten, no inventes de más):
   - un skill/checklist para **scaffolding de una sección CRUD nueva end-to-end** siguiendo el patrón canónico;
   - un skill de **design-system / consistencia visual** (iconos, tooltips, estilos) que apunte al catálogo del 9.2;
   - reforzar `desarrollador`/`arquitecto` para que **siempre** lean el estándar antes de codear y para que
     verifiquen paridad CM↔CP al tocar algo compartido;
   - un skill de **revisión de homogeneización** que reuse este mismo prompt.
   Para cada skill propuesto indicá: nombre, cuándo se dispara, qué docs carga, y un esbozo del contenido.

9.4 — **Anti-redundancia.** Identificá los puntos donde hoy los agentes se desvían (lo que el usuario tiene
   que aclarar repetido) y mapeá cada uno a "qué doc o skill lo resolvería de forma permanente".

### Formato de salida (obligatorio)

1. **Resumen ejecutivo** (5–10 líneas): estado general, build/tests, los 3–5 riesgos más graves, nivel de homogeneización CP↔CM.
2. **Tabla de hallazgos priorizada**, ordenada por severidad. Una fila por hallazgo:

   | # | Severidad | Categoría | Capa/Proyecto | Ubicación (`archivo:línea`) | Problema | Evidencia | Corrección recomendada | Esfuerzo |
   |---|-----------|-----------|---------------|------------------------------|----------|-----------|------------------------|----------|

   - **Severidad:** 🔴 Crítico (bug/dato/seguridad/AFIP incorrecto) · 🟠 Alto (incoherencia funcional / regla de arquitectura rota) · 🟡 Medio (homogeneización / deuda) · 🟢 Bajo (estilo/cosmético).
   - **Categoría:** Dependencias · Datos/EF · Negocio · AFIP · UI/MVVM · Homogeneización · Seguridad · Async/Recursos · Tests · Código muerto · Docs.
   - **Esfuerzo:** S / M / L.
3. **Sección "Homogeneización CP ↔ CM"**: lista específica de divergencias entre las dos apps y entre los dos dominios, con tabla `Artefacto A | Artefacto B | Diferencia | ¿Justificada?`.
4. **Catálogo de iconos y tooltips de operación**: tabla `Operación | Icono usado | ¿Consistente en toda la solución? | Tooltip | Páginas donde aparece | Inconsistencias`. Más una lista de botones de operación sin tooltip y/o sin icono.
5. **Paridad funcional CM → CP**: tabla `Funcionalidad | Estado CM | Estado CP | ¿Portado? | Diferencia/Acción`.
6. **Divergencias código ↔ documentación**: qué dice el doc y qué hace el código.
7. **Plan de corrección sugerido**: hallazgos agrupados en lotes lógicos (quick wins primero), en orden recomendado de ataque, pensado para ejecutarse manteniendo build verde + tests verdes tras cada lote.
8. **Plan de estandarización de documentación**: lista de documentos `doc/` a crear o actualizar (con ruta propuesta y un esquema/índice del contenido de cada uno), cubriendo los tres ejes diseño / negocio / implementación técnica. Marcá cuáles son nuevos y cuáles reemplazan/corrigen docs existentes.
9. **Plan de skills**: tabla `Skill | Nuevo o actualizar | Cuándo se dispara | Docs que carga | Qué resuelve (anti-redundancia)`, con el esbozo de contenido de cada skill nuevo.

### Reglas de la auditoría

- **No modifiques código existente ni docs existentes.** Para los entregables 8 y 9 podés **redactar borradores** de los documentos `doc/` y skills nuevos, pero entregalos como propuesta para aprobar (en el informe o como archivos nuevos claramente marcados como borrador), sin pisar nada existente. La aplicación real (crear/editar docs y skills, y corregir código) se hace en una pasada posterior, previa aprobación del usuario.
- **Todo hallazgo lleva evidencia** (`archivo:línea` y/o cita de código). Nada de afirmaciones genéricas sin respaldo.
- Si algo parece mal pero podría estar justificado por negocio, marcalo como **"a confirmar"** en vez de afirmarlo.
- Sé exhaustivo: recorré las 8 fases completas aunque la lista sea larga. No te detengas en la primera capa.
- Priorizá señal sobre ruido: agrupá hallazgos repetidos del mismo tipo en una fila con la lista de ubicaciones.
