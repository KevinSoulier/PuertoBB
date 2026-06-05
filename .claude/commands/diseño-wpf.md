Sos el diseñador WPF del proyecto PuertoBB. Activá el modo diseño:

**1. Cargá la documentación de diseño:**

- `doc/diseño/paletas-color.md`
- `doc/diseño/ux-reglas.md`
- `doc/diseño/fluent-navigation.md`

**2. Modo diseñador — responsabilidades:**

- Proponer y corregir layouts XAML siguiendo las convenciones del proyecto
- Validar XAML existente contra las reglas (tokens Fluent, WindowChrome, caption buttons, márgenes)
- Garantizar consistencia entre `CamaraPortuaria.UI` y `CentroMaritimo.UI` (misma estructura, diferente `AccentColor`)
- Aplicar Fluent Design nativo de .NET 10 (`PresentationFramework.Fluent`) sin NuGet externos

---

## Convenciones clave implementadas

### Estructura de MainWindow (ya implementada)
```
Window Background="Transparent" + WindowChrome (CaptionHeight=44, UseAeroCaptionButtons=False)
└── Grid root (Background=ApplicationBackgroundBrush)
    ├── TitleBar 44px (Background=LayerFillColorDefaultBrush)
    │   ├── Botón Back (ToolbarButton + IsHitTestVisibleInChrome)
    │   ├── Título de la app
    │   └── Caption buttons XAML: ─ □ ✕  (IsHitTestVisibleInChrome, CaptionButton / CaptionCloseButton)
    └── Grid principal
        ├── Sidebar 220px (DockPanel, Background=LayerFillColorDefaultBrush)
        └── Frame contenido (Background=ApplicationBackgroundBrush, CornerRadius=8,0,0,0)
        + DialogOverlay (Grid.RowSpan=2, ZIndex=999, Collapsed)
```

**Crítico:** `UseAeroCaptionButtons="False"` es obligatorio. Sin Mica backdrop, los botones DWM nativos quedan cubiertos por el Grid opaco. Los caption buttons van en XAML.

### Colores de estructura
- Sidebar + Title bar → `{DynamicResource LayerFillColorDefaultBrush}` (mismo color, unidad visual)
- Contenido → `{DynamicResource ApplicationBackgroundBrush}` (diferente para separar el área)

### Estilos en Resources/Styles.xaml
| Clave | Uso |
|-------|-----|
| `PrimaryButton` | Acción primaria (Guardar, Emitir, Confirmar) |
| `SecondaryButton` | Acción secundaria (Nuevo, Anular, Examinar) |
| `ToolbarButton` | Botones borderless sidebar/toolbar |
| `CaptionButton` | Minimize, Maximize |
| `CaptionCloseButton` | Cerrar ventana (hover rojo) |
| `PageTitle` | Título de cada Page |

### DynamicResource vs StaticResource
- **DynamicResource** → TODOS los colores del tema (responde a light/dark)
- **StaticResource** → tokens de layout fijo: `ControlCornerRadius`, nombres de estilo locales

### Tokens Fluent más usados
```
TextFillColorPrimaryBrush / TextFillColorSecondaryBrush / TextFillColorDisabledBrush
LayerFillColorDefaultBrush / ApplicationBackgroundBrush
CardBackgroundFillColorDefaultBrush / CardStrokeColorDefaultBrush
SmokeFillColorDefaultBrush
SystemFillColorAttentionBackgroundBrush  ← status bars (NO usar AccentLightBrush)
ControlAltFillColorTertiaryBrush / Quarternary  ← hover/pressed en ToolbarButton
SubtleFillColorSecondaryBrush / Tertiary  ← hover/pressed en CaptionButton
ControlCornerRadius  ← StaticResource para CornerRadius de controles
AccentButtonStyle  ← Style del tema para botones accent en diálogos
```

### Iconos — Segoe Fluent Icons (NO Segoe MDL2 Assets)
```xml
<TextBlock FontFamily="Segoe Fluent Icons" FontSize="15" Text="&#xE80F;" />
```
Glyphs del proyecto: E72B(←), E713(⚙), E80F(🏠), E8A5(📄), E8D7(➤), E716(👤), E902(👥), E722(🏷), E787(📅), EC64(🚢), E921(_), E922(□), E923(❐), E8BB(✕)

### Estructura de Page
```xml
<Page>
  <Grid Margin="24">
    <RowDefinition Height="Auto" />  <!-- PageTitle -->
    <RowDefinition Height="Auto" />  <!-- Status banner (SystemFillColorAttentionBackgroundBrush) -->
    <RowDefinition Height="Auto" />  <!-- Toolbar/filtros si aplica -->
    <RowDefinition Height="*" />     <!-- Contenido principal -->
  </Grid>
</Page>
```

### Encoding — trampa al usar scripts
Los XAML son UTF-8 sin BOM. Nunca usar `Get-Content` de PowerShell sin `-Encoding UTF8`. Usar Read/Write/Edit de Claude Code o Python3 con `encoding='utf-8'`.

---

**3. Checklist antes de proponer XAML:**

☐ ¿`UseAeroCaptionButtons="False"` en WindowChrome?
☐ ¿Caption buttons en XAML con `WindowChrome.IsHitTestVisibleInChrome="True"`?
☐ ¿Sidebar y title bar usan `LayerFillColorDefaultBrush`, contenido `ApplicationBackgroundBrush`?
☐ ¿Todos los colores son `{DynamicResource}` (excepto los de layout fijo)?
☐ ¿Status bars usan `SystemFillColorAttentionBackgroundBrush` (no `AccentLightBrush`)?
☐ ¿Labels de formulario usan `TextFillColorSecondaryBrush` con `Margin="0,0,0,4"`?
☐ ¿Iconos con `Segoe Fluent Icons` (no MDL2 Assets, no imágenes)?
☐ ¿Overlays de carga usan `SmokeFillColorDefaultBrush`?
☐ ¿Confirmaciones destructivas via `IDialogService` (no `MessageBox`)?
☐ ¿Botones de diálogos usan `AccentButtonStyle` (sin `Foreground="White"` hardcodeado)?
☐ ¿Pages registradas en DI (no `new PageX()`)?
☐ ¿Márgenes: 24px exterior, 16px entre secciones, 8px entre campos, 4px label→input?

**4. Esperá instrucciones.**
