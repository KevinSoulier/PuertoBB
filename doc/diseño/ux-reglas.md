# Reglas de UX y diseño WPF

> Última actualización: 2026-06-05

---

## Tema visual

> **Actualizado (D-14):** el proyecto usa la librería **WPF-UI** (NuGet `WPF-UI`), no el Fluent nativo.
> Detalle y mejores prácticas en [`fluent-wpfui.md`](fluent-wpfui.md). Notas clave:
> - Activación en `App.xaml`: `<ui:ThemesDictionary />` + `<ui:ControlsDictionary />`.
> - Dark/Light/System vía `ApplicationThemeManager` + `SystemThemeWatcher` (no `Application.ThemeMode`).
> - Transparencia **Mica** real con `ui:FluentWindow` (`WindowBackdropType="Mica"`).
> - Color de marca con `ApplicationAccentColorManager` (`App.AplicarAcentoMarca`, desde `Colors.xaml`).
> - Los resource keys WinUI (`{DynamicResource …}`) son los mismos, así que las reglas de abajo siguen vigentes.
- Sin Mica backdrop (el proyecto usa `ThemeMode`, no `EnableMicaBackdrop`)

---

## Layout general

```
Window (Background=Transparent) + WindowChrome
└── Grid root (Background=ApplicationBackgroundBrush)
    ├── Title bar 44px (Background=LayerFillColorDefaultBrush)
    │   ├── Botón Atrás (izquierda)
    │   ├── Nombre de la app (centro-izquierda)
    │   └── Caption buttons propios: ─ □ ✕ (derecha)
    └── Grid principal
        ├── Sidebar 220px (Background=LayerFillColorDefaultBrush)
        └── Frame contenido (Background=ApplicationBackgroundBrush, CornerRadius=8,0,0,0)
```

- Sidebar y title bar: mismo color `LayerFillColorDefaultBrush` → unidad visual
- Contenido: `ApplicationBackgroundBrush` → diferente para distinguir el área de trabajo
- Root Grid también en `ApplicationBackgroundBrush` → visible en el CornerRadius de la esquina

---

## WindowChrome y caption buttons

```xml
<WindowChrome CaptionHeight="44" ResizeBorderThickness="5"
              GlassFrameThickness="-1" UseAeroCaptionButtons="False" />
```

**`UseAeroCaptionButtons="False"` es obligatorio.** Sin Mica backdrop, los botones nativos del DWM quedan cubiertos por el root Grid opaco. Se usan botones XAML propios con `WindowChrome.IsHitTestVisibleInChrome="True"`.

Los estilos `CaptionButton` y `CaptionCloseButton` están en `Resources/Styles.xaml`.

---

## Márgenes y espaciado

| Contexto | Valor |
|----------|-------|
| Margin exterior de página (Grid raíz de cada Page) | 24px |
| Entre secciones dentro de una página | 16px |
| Entre label y su input | 4px (Margin bottom en el label) |
| Gap entre botones en toolbar o grupo | 8px — `Margin="0,0,8,8"` en **cada** botón (6px → `0,0,6,6` en grillas). Ver "Filas de botones". |
| Padding interno de botones Primary/Secondary | 16,8 |
| Padding interno de botones ToolbarButton | 8,4 |

---

## Estilos de botones — cuándo usar cada uno

| Estilo | Cuándo | Ejemplo |
|--------|--------|---------|
| `{StaticResource PrimaryButton}` | Acción primaria de la vista | Guardar, Emitir, Confirmar, Cerrar período |
| `{StaticResource SecondaryButton}` | Acciones secundarias, destructivas | Nuevo, Eliminar, Anular, Examinar, Backup |
| `{StaticResource ToolbarButton}` | Botones de sidebar y otros toolbar borderless | Configuración (sidebar), Back button |
| `{StaticResource CaptionButton}` | Solo caption buttons de ventana | Minimizar, Maximizar |
| `{StaticResource CaptionCloseButton}` | Solo caption button cerrar | Cerrar ventana |
| `{DynamicResource AccentButtonStyle}` | Botones accent en diálogos | Aceptar, Confirmar (en Dialogs) |

**Botones sin estilo explícito** (`<Button Content="..." />`) heredan el tema Fluent por defecto — válido cuando el botón no necesita padding especial.

---

## Filas de botones (wrap, no recortar)

Las filas de botones y de acciones usan **`WrapPanel`**, nunca `StackPanel Orientation="Horizontal"`.
Con `StackPanel` horizontal, cuando los botones no entran en el ancho disponible (típico en columnas de
ancho fijo, ej. 360/380px) el último se **recorta**; con `WrapPanel` los botones sobrantes pasan a un
segundo renglón y el contenido se desplaza hacia abajo.

- Cada botón lleva `Margin="0,0,8,8"` (derecha = gap horizontal entre botones; abajo = gap vertical al
  envolver). En celdas de DataGrid usar `0,0,6,6`.
- Aplica también a **filas mixtas** (filtros + botones): el contenedor pasa a `WrapPanel` y cada grupo
  lógico de nivel superior (cada `label+control` agrupado en su `StackPanel` interno, y cada botón) lleva
  `Margin="0,0,8,8"`, para que envuelva sin separar un control de su etiqueta.
- **No** aplica a: caption buttons / navegación de `MainWindow`, ni a los `StackPanel` que son **contenido
  de un botón** (ícono + texto) o tiles del Dashboard (`UniformGrid`).
- Pies de diálogo (Cancelar/Aceptar): `WrapPanel HorizontalAlignment="Right"`, conservando el gap entre los
  dos botones (primer botón `0,0,8,0`).

```xml
<!-- ✓ Correcto -->
<WrapPanel>
    <ui:Button Content="Nuevo"   Style="{StaticResource AccionIconButton}" Margin="0,0,8,8" />
    <ui:Button Content="Editar"  Style="{StaticResource AccionIconButton}" Margin="0,0,8,8" />
    <ui:Button Content="Eliminar" Style="{StaticResource AccionIconButton}" Margin="0,0,8,8" />
</WrapPanel>
```

---

## Colores — reglas

### Usar `{DynamicResource}` para TODOS los colores del tema
Esto hace que la UI responda automáticamente al cambio de tema (light/dark).

```xml
<!-- ✓ Correcto -->
Foreground="{DynamicResource TextFillColorSecondaryBrush}"
Background="{DynamicResource SystemFillColorAttentionBackgroundBrush}"

<!-- ✗ Incorrecto — no adapta a dark mode -->
Foreground="#6B6B6B"
Background="#E3F2FD"
```

### `{StaticResource}` solo para tokens de layout fijo
```xml
<!-- ✓ Correcto — el CornerRadius no cambia con el tema -->
CornerRadius="{StaticResource ControlCornerRadius}"
```

### Colores hexadecimales permitidos
- `#C42B1C` y `#A3211A` — hover rojo en `CaptionCloseButton` (convención Windows)
- `Foreground="White"` sobre fondos de acento en title bar y caption close hover

Ver lista completa en `doc/diseño/paletas-color.md`.

---

## CornerRadius — reglas

| Contexto | Valor |
|----------|-------|
| Controles (botones, inputs) | `{StaticResource ControlCornerRadius}` |
| Cards/panels dentro de vistas | `"8"` (uniforme, explícito) |
| Status bar / banners | `{StaticResource ControlCornerRadius}` |
| Content frame (unión sidebar-contenido) | `"8,0,0,0"` (solo esquina superior izquierda) |
| Diálogos | `"8"` |

---

## Iconos

Usar **Segoe Fluent Icons** (NOT Segoe MDL2 Assets). FontFamily en el TextBlock, no en el Button.

```xml
<TextBlock FontFamily="Segoe Fluent Icons" FontSize="15" Text="&#xE80F;" />
```

Ver tabla completa de glyphs en `doc/diseño/fluent-navigation.md` sección 6.

---

## Status bars (banners de mensaje en vistas)

```xml
<Border Padding="12,8"
        CornerRadius="{StaticResource ControlCornerRadius}"
        Background="{DynamicResource SystemFillColorAttentionBackgroundBrush}"
        Visibility="{Binding HasStatus, Converter={StaticResource BoolToVisibilityConverter}}">
    <TextBlock Text="{Binding StatusMessage}" TextWrapping="Wrap" />
</Border>
```

**No usar `AccentLightBrush` para status bars** — es un color de marca fijo que no adapta a dark mode.

---

## Labels de formulario

Los labels de formulario usan `TextFillColorSecondaryBrush` para jerarquía visual:

```xml
<TextBlock Text="Nombre" Foreground="{DynamicResource TextFillColorSecondaryBrush}" Margin="0,0,0,4" />
<TextBox Text="{Binding NombreEdit, UpdateSourceTrigger=PropertyChanged}" Margin="0,0,0,8" />
```

Los section headers (`FontWeight="SemiBold"`) no necesitan Foreground explícito — heredan `TextFillColorPrimaryBrush`.

---

## Overlays de carga

Las esperas usan el **control reutilizable `BusyOverlay`** (en `Controls/BusyOverlay.xaml` de cada
app): un fondo atenuado (`SmokeFillColorDefaultBrush`, `Opacity=0.6`) con una **tarjeta sólida y
compacta** centrada — mismo lenguaje visual que los diálogos modales (`SolidBackgroundFillColorBaseBrush`,
`CornerRadius=8`, `ElevationShadow16Effect`) — que muestra `ui:ProgressRing`, título, entidad actual,
barra de progreso, contador y botón Cancelar. **No** hardcodear `Foreground="White"`: la tarjeta es sólida
y el texto usa tokens del tema (`TextFillColorPrimaryBrush`/`Secondary`), así adapta a light/dark.

Se coloca como último hijo del contenedor raíz del Page, cubriendo el área de trabajo:

```xml
<!-- xmlns:controls="clr-namespace:<App>.Controls" -->
<controls:BusyOverlay Grid.RowSpan="N" />
```

El estado lo provee `PageViewModel` (hereda el DataContext del Page). **No** setear `IsBusy` ni el
overlay a mano: ejecutar las operaciones por los helpers de `PageViewModel`:

- `EjecutarOcupadoAsync(titulo, operacion)` — operación corta/de un paso: spinner indeterminado, sin
  contador ni cancelar (ej. "Cargando recibos", "Generando PDF", "Generando backup").
- `EjecutarConProgresoAsync(titulo, (progreso, ct) => …)` — operación masiva: barra determinada +
  contador "N / M" + botón Cancelar. El servicio recibe `IProgress<ProgresoMasivo>` y `CancellationToken`
  y reporta el avance por ítem (`progreso.Report(new ProgresoMasivo(actual, total, entidad))`). La variante
  genérica devuelve el resultado del servicio, o `null` si el usuario canceló.

---

## Validación

- Validación en **tiempo real** con mensajes bajo el campo, nunca en popup
- Los mensajes de error de validación van debajo del control que los originó

---

## Estados de carga y mensajes de resultado

- Overlays `ProgressBar` durante operaciones asíncronas (`IsBusy`)
- Mensajes de error/éxito vía **Snackbar toast** (`MostrarError` / `MostrarExito` en `PageViewModel`) — no en dialogs ni en status bars estáticas
- Confirmaciones destructivas (eliminar, anular) **siempre en dialog modal** via `IDialogService`

---

## Diálogos modales

WPF no tiene `ContentDialog` built-in. Los diálogos se inyectan en el `DialogOverlay` de `MainWindow`. Ver implementación completa en `doc/diseño/fluent-navigation.md` sección 9.

| Tipo | Cuándo usar |
|------|-------------|
| `ConfirmDialog` | Acciones destructivas que requieren confirmación |
| `AlertDialog` | Errores, advertencias, éxito con un botón |
| `InputDialog` | Captura de valor rápida (nombre, motivo, etc.) |

- Siempre vía `IDialogService` inyectado en el ViewModel — **nunca** instanciar dialogs directamente
- `Foreground="White"` no debe aparecer en botones de diálogos: usar `AccentButtonStyle` que lo define

---

## Inyección de dependencias (DI)

- Las `Page` se registran como `Transient` en DI — nunca instanciar con `new`
- `INavigationService` → `Singleton`; `MainWindow` y `MainWindowViewModel` → `Singleton`
- Sin lógica de negocio en code-behind: solo `InitializeComponent()` y wire-up de eventos de nav

---

## Encoding — trampa en scripts

Los archivos XAML son UTF-8 sin BOM. **Nunca usar PowerShell `Get-Content`/`Set-Content` sin encoding explícito** — dobla el encoding de caracteres no-ASCII.

```powershell
# ✗ PELIGROSO: dobla encoding (ó → Ã³)
(Get-Content $file -Raw) -replace ... | Set-Content $file -Encoding UTF8

# ✓ SEGURO
(Get-Content $file -Encoding UTF8 -Raw) -replace ... | Set-Content $file -Encoding UTF8NoBOM
```

Alternativa segura: Python3 con `encoding='utf-8-sig'` para leer y `encoding='utf-8'` para escribir.
Las herramientas Read/Write/Edit de Claude Code son seguras.

---

## Prohibiciones

- **Nunca `MessageBox` nativo.** Siempre via `IDialogService`.
- Sin fuentes externas (Google Fonts, etc.) en XAML.
- Sin colores hexadecimales en XAML — solo los de `Resources/Colors.xaml` o tokens Fluent.
- Sin imágenes para iconos de navegación — solo glifos de Segoe Fluent Icons.
- Sin `FontFamily="Segoe UI"` explícito en DataGrids/ListBoxes/TreeViews — el tema Fluent lo aplica automáticamente.
