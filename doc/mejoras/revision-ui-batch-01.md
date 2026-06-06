# Revisión UI/UX — Batch 01

> **Estado:** ✅ Implementado (2026-06-05). Documento vivo — agregar nuevos hallazgos abajo.
> Acá se listan los cambios detectados. **Todavía no se diseñan ni implementan** — eso es la siguiente etapa.
> Fecha de relevamiento: 2026-06-05.

## Contexto

Sesión de revisión de la plataforma PuertoBB para juntar **todo lo que hace falta modificar** en la UI/UX
antes de diseñar e implementar el batch de cambios. El documento se irá ampliando a medida que aparezcan
más cosas ("luego seguimos con otras cosas que encuentre").

La solución tiene **dos aplicaciones WPF** que comparten Core/Services/Infrastructure:

| App | Proyecto | Secciones actuales |
|-----|----------|--------------------|
| **Centro Marítimo** | `CentroMaritimo.UI` | Inicio, Vouchers, Cierre de período, Recibos, Emisión masiva, Agencias, Barcos, Grupos, Configuración |
| **Cámara Portuaria** | `CamaraPortuaria.UI` | Inicio, Recibos, Emisión masiva, Empresas, Grupos, Configuración |

Stack: WPF-UI 4.3 (Fluent / Mica), net10.0-windows, MVVM, EF Core + SQLite.

### Convenciones

- Cada ítem tiene un **ID** (`AREA-n`) para referenciarlo al diseñar/implementar.
- **App:** `CM` = Centro Marítimo · `CP` = Cámara Portuaria · `Ambas` = aplica a las dos (en CP, "Agencias" ≈ "Empresas").
- **Estado actual:** lo que verificamos en el código durante el relevamiento (con archivo de referencia).
- **A decidir:** preguntas abiertas para la etapa de diseño.

---

## 0. Cambios globales (transversales)

### GLB-1 — Renombrar "Centro de Navegación" → "Centro Marítimo"
- **App:** CM
- **Qué:** El nombre de la aplicación es **Centro Marítimo**, no "Centro de Navegación".
- **Estado actual:** Quedan 2 lugares con el nombre viejo:
  - `CentroMaritimo.UI/MainWindow.xaml:35` → `Title="Centro de Navegación de Bahía Blanca"`.
  - `PuertoBB.Services/Pdf/CentroMaritimoPdfService.cs:13` → `Titulo = "Centro de Navegación / Marítimo"`.
- **A decidir:** texto exacto final en TitleBar y en el encabezado de los PDF.

### GLB-2 — Sistema de validadores reutilizable
- **App:** Ambas
- **Qué:** Revisar/introducir validadores que se puedan **extender a toda la app** (no validación ad-hoc por pantalla).
- **Estado actual:** **No hay validadores formales.** No se usa `IDataErrorInfo`, `INotifyDataErrorInfo`,
  `FluentValidation` ni `DataAnnotations`. La validación es manual en cada ViewModel + Service
  (ej. `VouchersViewModel.CrearAsync` y `VoucherService.CrearVoucherAsync`), con mensajes vía `ServiceResult<T>`
  (`PuertoBB.Core/Common/ServiceResult.cs`).
- **A decidir:** enfoque (INotifyDataErrorInfo en una base de ViewModel vs. FluentValidation vs. reglas WPF),
  dónde viven las reglas (Core vs UI), y cómo se muestran los errores en Fluent. Definir esto **primero** porque
  varios ítems de abajo (importe, numeración, mails, CUIT) dependen de él.

### GLB-3 — Estilo de DatePicker (se ve "muy transparente")
- **App:** Ambas (hoy solo aparece en Vouchers de CM)
- **Qué:** El date picker se ve demasiado transparente; darle un estilo Fluent legible.
- **Estado actual:** No hay estilo propio de `DatePicker`; usa el default de WPF sobre el tema Fluent.
  Único uso en `CentroMaritimo.UI/Views/VouchersPage.xaml` (~línea 52). Estilos globales en
  `CentroMaritimo.UI/Resources/Styles.xaml` (no define DatePicker).
- **A decidir:** crear `DatePickerStyle` global (¿en cada UI o en un diccionario compartido?). Ojo: lo que se agregue
  a Vouchers también puede cambiar según VOU-4 (período mes/año).

### GLB-4 — Diálogo de confirmación demasiado transparente
- **App:** Ambas
- **Qué:** El pop-up de confirmación de eliminación es muy transparente. Que **no** use transparencia (o use otro
  estilo) y siga el look Fluent del resto.
- **Estado actual:** `ConfirmDialog`/`AlertDialog` son UserControls custom
  (`CentroMaritimo.UI/Dialogs/`). El overlay está en `MainWindow.xaml:52-55` con
  `Rectangle Fill="{DynamicResource SmokeFillColorDefaultBrush}" Opacity="0.6"`. La tarjeta usa
  `CardBackgroundFillColorDefaultBrush`. La transparencia percibida viene del overlay/Mica detrás de la tarjeta.
- **A decidir:** subir opacidad del scrim y/o dar fondo sólido (no Mica) a la tarjeta del diálogo.

### GLB-5 — Agrupar secciones en el sidebar
- **App:** Ambas
- **Qué:** Agrupar el NavigationView por temas, **dejando Inicio y Configuración sueltos**. Ej. propuesto:
  - **Comprobantes:** Vouchers + Cierre de período (consolidación) + Recibos + Emisión masiva.
  - **Maestros:** Agencias + Barcos + Grupos.
  - (Ver SEP-1: Control de pagos pasaría a ser su propia sección, fuera de Inicio.)
- **Estado actual:** Navegación plana, definida en
  `CentroMaritimo.UI/ViewModels/MainWindowViewModel.cs` y `CamaraPortuaria.UI/ViewModels/MainWindowViewModel.cs`.
- **A decidir:** nombres y composición exactos de cada grupo en **cada app** (CP no tiene Vouchers/Barcos; tiene
  Empresas en vez de Agencias). Mecanismo en WPF-UI: `NavigationViewItemHeader` (encabezados) vs. ítems
  jerárquicos con `MenuItems`. Confirmar si "Cierre de período" = consolidación.

### GLB-6 — Pantalla de Inicio con dashboard / accesos
- **App:** Ambas
- **Qué:** Que Inicio tenga un dashboard real (métricas/resumen) o, como mínimo, botones que lleven a cada sección.
- **Estado actual:** Hoy `DashboardPage` **es "Control de pagos"** (listado de recibos pendientes/vencidos), no un
  inicio. Es la página a la que se navega al arrancar (`MainWindow.xaml.cs`). Ver `Views/DashboardPage.xaml`.
- **Depende de:** SEP-1 (separar Control de pagos para liberar el Inicio).

### SEP-1 — "Control de pagos" como sección propia
- **App:** Ambas
- **Qué:** Control de pagos debe ser **una sección aparte** (como Vouchers o Agencias), no el contenido de Inicio.
- **Estado actual:** "Control de pagos" está embebido en `DashboardPage` y ocupa el lugar de Inicio.
- **A decidir:** crear `ControlPagosPage` (mover el contenido actual del Dashboard) y reconstruir `DashboardPage`
  como Inicio (GLB-6). Definir ítem de sidebar e ícono. Aplica a ambas apps.

---

## 1. Vouchers  ·  App: CM

Archivos: `Views/VouchersPage.xaml`, `ViewModels/VouchersViewModel.cs`,
`ViewModels/Items/VoucherItem.cs`, `PuertoBB.Services/Negocio/VoucherService.cs`.

### VOU-1 — DatePicker transparente
- Mismo problema que **GLB-3**. El date picker de Vouchers es el caso concreto a corregir.

### VOU-2 — Barco con autocompletado + alta inline
- **Qué:** Poder **escribir** el nombre del barco con autocompletado (combo editable): seleccionar uno existente
  o, si no existe, **crear uno nuevo** desde el mismo control. Búsqueda parcial, tolerante a mayúsc./minúsc.
- **Aclaración del usuario:** los barcos no necesitan consistencia por ID; es solo para buscar/cargar más rápido
  ("con teclar unas pocas letras aparece si ya se usó").
- **Estado actual:** ComboBox **no editable**, `DisplayMemberPath="Nombre"`. Carga todos los barcos ordenados por
  nombre (`VouchersViewModel`). Entidad `Barco` tiene índice único en `Nombre`
  (`PuertoBB.Core/Entities/CentroMaritimo/Barco.cs`); repo expone `GetPorNombreAsync`. FK `Voucher.BarcoId` con
  `DeleteBehavior.Restrict`.
- **A decidir:** control a usar (ComboBox `IsEditable` con filtrado vs. control autosuggest custom — hoy no existe
  ninguno). Política de normalización de mayúsculas al crear. Match parcial case-insensitive.

### VOU-3 — Agencia con autocompletado (sin alta inline) + botón "＋"
- **Qué:** Igual que VOU-2 para la **agencia** (escribir + autocompletar + dropdown), pero **sin** crear agencia
  nueva desde acá. Agregar un botón **＋** que lleve a la pantalla de Agencias a dar de alta.
- **Estado actual:** ComboBox no editable; carga solo agencias **activas** (`GetActivasAsync()`).
- **A decidir:** comportamiento del ＋ (navegar a Agencias; ¿volver con la agencia nueva preseleccionada?).
  Relación con AGE-3 (quitar "Activa"): si se quita, cambia el filtro de carga.

### VOU-4 — Período: mes por nombre + año con escritura / ＋–
- **Qué:** En los dropdowns de período, el **mes** debe mostrar el **nombre** (Enero, Febrero…). El **año** no debe
  ser dropdown: permitir **escribirlo** o ajustarlo con botones **＋ / –**.
- **Estado actual:** período = ComboBox de enteros (Meses, Años). Ya existe `Formato.Periodo(anio, mes)` →
  "Enero 2026" en `PuertoBB.Services/Common/Formato.cs` (reutilizable para nombres de mes).
- **Nota:** En Vouchers el período hoy se **deriva de la fecha**; confirmar si el control de período va en Vouchers,
  en Emisión masiva, o en ambos. (En Emisión masiva sí hay mes/año explícitos.)

### VOU-5 — Importe autopopulado (editable)
- **Qué:** Que el monto del voucher venga **predefinido** y autopopulado para agilizar la carga, pudiéndose cambiar.
- **Estado actual:** `Importe` es obligatorio, sin default; validación `> 0` en VM y Service. No hay fuente de un
  importe por defecto.
- **A decidir:** **de dónde sale el valor por defecto** (monto fijo en Configuración / último importe usado /
  importe por agencia o barco). Definir antes de implementar.

### VOU-6 — Numeración del voucher editable
- **Qué:** Poder **modificar la numeración** para arrancar desde un número más alto si hace falta (migración del
  sistema manual).
- **Estado actual:** Número autogenerado por `ContadorVoucher` singleton (`UltimoNumero`), incremento en
  `ContadorVoucherRepository.ObtenerSiguienteNumeroAsync`. Seed `UltimoNumero = 0`. El usuario **no** ve ni edita
  el número. La entidad ya está pensada para editarse ("editable para migrar del sistema manual").
- **A decidir:** dónde se edita (¿Configuración?), validaciones (no pisar números ya usados; índice único en
  `Voucher.Numero`).

### VOU-7 — Editar un voucher ya creado
- **Qué:** Permitir editar un voucher existente.
- **Estado actual:** Existe `ActualizarVoucherAsync`, pero **solo si no está consolidado** (`ReciboId == null`).
  El DataGrid es `IsReadOnly` (sin edición inline). No hay UI de edición.
- **A decidir:** UI de edición (panel/diálogo), qué campos se pueden editar, y qué pasa con consolidados.

### VOU-8 — Ver si el voucher ya se emitió / imprimió
- **Qué:** Que se pueda ver si el voucher ya fue emitido/impreso.
- **Estado actual:** No hay campo "emitido"/"impreso". Solo se infiere "consolidado" por `ReciboId != null`
  (`VoucherItem.Consolidado`).
- **A decidir:** ¿"emitido/impreso" = consolidado, o es un estado nuevo? Si es nuevo, requiere campo en entidad +
  migración + indicador visual.

---

## 2. Agencias  ·  App: CM  (en CP: análogo en **Empresas**)

Archivos: `Views/AgenciasPage.xaml`, `ViewModels/AgenciasViewModel.cs`.

### AGE-1 — Buscador único multi-campo
- **Qué:** Un **solo** buscador que filtre por **nombre, razón social o CUIT** (cualquier campo que contenga el texto).
- **Estado actual:** **No hay buscador** en la UI; el DataGrid muestra todo (`GetTodasConEmailsAsync()`).
- **A decidir:** filtrado en memoria vs. repo; debounce.

### AGE-2 — Campo de mails con scroll
- **Qué:** Que el campo de mail del CRUD tenga scroll.
- **Estado actual:** El TextBox de Emails **ya** tiene `Height="80"`, `TextWrapping="Wrap"`,
  `VerticalScrollBarVisibility="Auto"`. **Verificar en runtime** si el problema persiste (quizás faltan alto o el
  scroll no aparece como se espera). Posible "ya resuelto" / ajuste menor.

### AGE-3 — Quitar "Activo/a" de agencias
- **Qué:** No necesitamos el valor Activo/a en la agencia.
- **Estado actual:** `Agencia.Activa` existe; se muestra en DataGrid y ficha; **se usa para filtrar** en
  `GetActivasAsync()` (lo consume Vouchers VOU-3 y Emisión masiva).
- **A decidir / impacto:** quitarlo implica revisar todos los usos de `GetActivasAsync()` / `Activa` (Vouchers,
  Emisión masiva, repos, posible migración). Confirmar alcance antes de eliminar el campo vs. solo ocultarlo en UI.

### AGE-4 — Diálogo de eliminación transparente
- Mismo problema que **GLB-4**.

---

## 3. Barcos  ·  App: CM

Archivos: `Views/BarcosPage.xaml`, `ViewModels/BarcosViewModel.cs`.

### BAR-1 — Buscador + orden alfabético
- **Qué:** Agregar buscador y que la lista quede ordenada alfabéticamente.
- **Estado actual:** Sin buscador. Ya ordena por nombre (`OrderBy(b => b.Nombre)` en `CargarAsync`). Falta el buscador.

### BAR-2 — Estilo de la lista inconsistente
- **Qué:** La lista de barcos no sigue el mismo estilo; revisar los elementos.
- **Estado actual:** Usa DataGrid (como Agencias), pero el panel derecho es más angosto (Width 320 vs 360 en
  Agencias) y la vista es más "pelada" (solo columna Nombre, panel solo con TextBox). Revisar paddings, anchos,
  encabezados y estados vacíos para alinearlo con las otras páginas.
- **A decidir:** definir el patrón "canónico" de página maestro-detalle y aplicarlo.

---

## 4. Grupos  ·  App: Ambas

Archivos: `Views/GruposPage.xaml`, `ViewModels/GruposViewModel.cs`,
`ViewModels/Items/MiembroGrupoItem.cs`.

### GRU-1 — Quitar estado "Activo"
- **Qué:** No se necesita el estado de activo en grupos.
- **Estado actual:** Existe CheckBox `ActivoEdit`. Emisión masiva carga solo grupos activos (`GetActivosAsync()`).
- **A decidir / impacto:** revisar usos de "activo" en Emisión masiva y repos antes de quitar.

### GRU-2 — Miembros: scroll + layout que se adapta al alto
- **Qué:** La sección de miembros debe tener scroll y **adaptarse al alto de la pantalla**: los botones **no** deben
  irse hacia abajo si la ventana es corta; en todo caso se achica la lista de miembros (con scroll) y los botones
  quedan visibles.
- **Estado actual:** La lista de miembros es `ListBox` en `Grid.Row="2"` (`Height="*"`, con scroll nativo) y los
  botones en `Grid.Row="3"` (`Height="Auto"`, fijos al pie). **En teoría ya cumple**; verificar en runtime con
  ventana corta por si algún contenedor padre fuerza el desborde.

### GRU-3 — Buscar miembros
- **Qué:** Poder buscar/filtrar los miembros a seleccionar.
- **Estado actual:** **No hay** búsqueda de miembros.
- **A decidir:** buscador sobre la lista de miembros (filtrado en memoria), manteniendo selección.

---

## 5. Emisión masiva  ·  App: Ambas

Archivos: `Views/EmisionMasivaPage.xaml`, `ViewModels/EmisionMasivaViewModel.cs`.

### EMI-1 — Previa del estado antes de emitir
- **Qué:** Al seleccionar un grupo, **mostrar el estado de lo que se va a generar**: qué recibos se generarían, qué
  ya está generado y qué no — para no clickear "Emitir" a ciegas.
- **Estado actual:** Hoy se emite "a ciegas": al apretar Emitir recién chequea duplicados
  (`GetDuplicadosAsync()`), pide confirmación y emite; los resultados se ven **después**. No hay vista previa.
- **A decidir:** diseñar panel de previsualización (por agencia/empresa del grupo en el período: estado
  pendiente / ya emitido / a generar) que se calcule al elegir grupo+período, antes de emitir.

---

## 6. Inicio / Control de pagos  ·  App: Ambas

Cubierto por **GLB-6** (Inicio con dashboard/accesos) y **SEP-1** (Control de pagos como sección propia).
Ver esos ítems.

---

## Matriz de aplicabilidad por app

| ID | Tema | CM | CP |
|----|------|----|----|
| GLB-1 | Renombrar Centro Marítimo | ✅ | — |
| GLB-2 | Validadores reutilizables | ✅ | ✅ |
| GLB-3 | DatePicker estilo | ✅ | ✅ (si aplica) |
| GLB-4 | Diálogo no transparente | ✅ | ✅ |
| GLB-5 | Agrupar sidebar | ✅ | ✅ (sin Vouchers/Barcos) |
| GLB-6 | Inicio dashboard/accesos | ✅ | ✅ |
| SEP-1 | Control de pagos sección propia | ✅ | ✅ |
| VOU-1..8 | Vouchers | ✅ | — (CP no tiene Vouchers) |
| AGE-1..4 | Agencias | ✅ | ✅ → en **Empresas** |
| BAR-1..2 | Barcos | ✅ | — (CP no tiene Barcos) |
| GRU-1..3 | Grupos | ✅ | ✅ |
| EMI-1 | Emisión masiva previa | ✅ | ✅ |

> **Nota CP:** Cámara Portuaria no maneja Vouchers ni Barcos. Lo de Agencias se traslada a **Empresas**
> (`CamaraPortuaria.UI/Views/EmpresasPage.xaml`). Validar caso por caso al diseñar.

---

## Pendientes / a relevar en próximas pasadas

- Confirmar agrupación exacta del sidebar por app (nombres de grupos, íconos, orden).
- Definir fuente del importe por defecto del voucher (VOU-5).
- Definir si "emitido/impreso" es estado nuevo o equivale a consolidado (VOU-8).
- Decidir enfoque de validadores (GLB-2) — es prerequisito de varios ítems.
- Verificar en runtime AGE-2 y GRU-2 (podrían estar ya resueltos / ajuste menor).
- (El usuario seguirá agregando ítems a medida que encuentre más cosas.)
