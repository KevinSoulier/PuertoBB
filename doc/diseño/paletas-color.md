# Paletas de color

> Estado: **Actualizado** (2026-06-05)
> Los colores "neutrales compartidos" fueron eliminados de Colors.xaml — esos roles los cubren los tokens del tema.
>
> **D-14:** el tema lo provee ahora la librería **WPF-UI** (no `PresentationFramework.Fluent`), pero usa los
> **mismos resource keys WinUI**, así que esta guía sigue válida. El `AccentColor` de marca (CP `#1565C0`,
> CM `#00695C`) se inyecta como acento de la app con `ApplicationAccentColorManager`
> (`App.AplicarAcentoMarca`) — así el indicador del `NavigationView` y los botones primarios usan la marca.
> Ver [`fluent-wpfui.md`](fluent-wpfui.md).

---

## Principio: usar tokens Fluent, no colores propios

El 90% de los colores que necesita la UI los provee el tema Fluent vía `{DynamicResource}`. Definen colores propios **solo** los colores de marca y de dominio que el tema no conoce.

Usar `{DynamicResource NombreToken}` para que los colores respondan automáticamente a light/dark mode.

---

## Colors.xaml — colores definidos por el proyecto

### Cámara Portuaria (`CamaraPortuaria.UI/Resources/Colors.xaml`)

| Clave | Valor | Uso |
|-------|-------|-----|
| `AccentColor` | `#1565C0` | Color de marca (azul marino) |
| `AccentLightColor` | `#E3F2FD` | Tinte claro del color de marca |
| `AccentBrush` | derivado | Fondo de title bar en código CS |
| `AccentLightBrush` | derivado | — remanente histórico, no usar en vistas |

### Centro Marítimo (`CentroMaritimo.UI/Resources/Colors.xaml`)

| Clave | Valor | Uso |
|-------|-------|-----|
| `AccentColor` | `#00695C` | Color de marca (verde teal) |
| `AccentLightColor` | `#E0F2F1` | Tinte claro del color de marca |
| `AccentBrush` | derivado | — |
| `AccentLightBrush` | derivado | — |

### Colores de estado de recibo (ambas apps)

Definidos en `EstadoReciboToBrushConverter.cs` (en código, no en XAML) y como referencia en Colors.xaml:

| Estado | Fondo | Clave en Colors.xaml |
|--------|-------|----------------------|
| Pendiente | `#FFF3E0` | `EstadoPendienteBrush` |
| Emitido | `#E3F2FD` | `EstadoEmitidoBrush` |
| Enviado | `#E0F7FA` | `EstadoEnviadoBrush` |
| Pagado | `#E8F5E9` | `EstadoPagadoBrush` |
| Vencido | `#FFEBEE` | `EstadoVencidoBrush` |
| Incobrable | `#FBE9E7` | — (reemplaza al viejo "Moroso") |
| Anulado | `#F5F5F5` | `EstadoAnuladoBrush` |

---

## Tokens Fluent para usar en XAML (siempre `{DynamicResource}`)

### Texto

| Token | Uso |
|-------|-----|
| `TextFillColorPrimaryBrush` | Texto principal, títulos |
| `TextFillColorSecondaryBrush` | Labels de formulario, subtítulos |
| `TextFillColorDisabledBrush` | Texto cuando control está deshabilitado |
| `TextFillColorInverseBrush` | Texto sobre fondo de acento/color |

### Fondos estructurales

| Token | Uso |
|-------|-----|
| `ApplicationBackgroundBrush` | Fondo base de la ventana, área de contenido |
| `LayerFillColorDefaultBrush` | Sidebar, title bar, paneles elevados |
| `CardBackgroundFillColorDefaultBrush` | Diálogos, tarjetas flotantes |

### Estados / notificaciones

| Token | Uso |
|-------|-----|
| `SystemFillColorAttentionBackgroundBrush` | Status bar informativa (adapta a light/dark) |
| `SystemFillColorSuccessBrush` | Icono/indicador de éxito |
| `SystemFillColorCautionBrush` | Icono/indicador de advertencia |
| `SystemFillColorCriticalBrush` | Icono/indicador de error |
| `SmokeFillColorDefaultBrush` | Overlay semitransparente de diálogos |

### Bordes

| Token | Uso |
|-------|-----|
| `CardStrokeColorDefaultBrush` | Borde de diálogos y tarjetas |

### Interacción (botones y controles hover/pressed)

| Token | Uso |
|-------|-----|
| `ControlAltFillColorTertiaryBrush` | Hover en ToolbarButton |
| `ControlAltFillColorQuarternaryBrush` | Pressed en ToolbarButton |
| `SubtleFillColorSecondaryBrush` | Hover en CaptionButton (min/max) |
| `SubtleFillColorTertiaryBrush` | Pressed en CaptionButton |

### Layout fijos (usar `{StaticResource}` — no cambian con el tema)

| Token | Uso |
|-------|-----|
| `ControlCornerRadius` | CornerRadius estándar de controles y status bars |

---

## Colores que YA NO están en Colors.xaml

Estos colores fueron eliminados porque duplicaban tokens Fluent. Si los ves en código antiguo, reemplazarlos por el token correspondiente:

| Clave eliminada | Reemplazar con |
|-----------------|----------------|
| `TextPrimaryBrush` / `TextPrimaryColor` | `{DynamicResource TextFillColorPrimaryBrush}` |
| `TextSecondaryBrush` / `TextSecondaryColor` | `{DynamicResource TextFillColorSecondaryBrush}` |
| `BackgroundBrush` / `BackgroundColor` | `{DynamicResource ApplicationBackgroundBrush}` |
| `SurfaceBrush` / `SurfaceColor` | `{DynamicResource LayerFillColorDefaultBrush}` |
| `BorderBrush` / `BorderColor` | `{DynamicResource CardStrokeColorDefaultBrush}` |
| `SuccessBrush` / `SuccessColor` | `{DynamicResource SystemFillColorSuccessBrush}` |
| `WarningBrush` / `WarningColor` | `{DynamicResource SystemFillColorCautionBrush}` |
| `ErrorBrush` / `ErrorColor` | `{DynamicResource SystemFillColorCriticalBrush}` |
