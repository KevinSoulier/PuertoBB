# Fluent Design + Navegación lateral — Guía de implementación

> ⚠️ **SUPERADO (2026-06-05) por [`fluent-wpfui.md`](fluent-wpfui.md).** Este documento describe el
> stack anterior (Fluent **nativo** + `TreeView`/`Frame` + `WindowChrome` + `Application.ThemeMode`).
> Se adoptó la librería **WPF-UI** (`ui:FluentWindow` + `ui:NavigationView` + `ui:SymbolIcon`, Mica real,
> `ApplicationThemeManager`). Ver decisión **D-14** en `doc/decisiones/registro-decisiones.md`.
> Se conserva como referencia histórica del enfoque previo.

> Estado: histórico — stack reemplazado.
> Referencia de código: `CamaraPortuaria.UI/` y `CentroMaritimo.UI/`
> Proyecto de referencia externo: `C:\Users\kevin\source\repos\CamaraPortuariaBB\CamaraPortuariaBB.App`

---

## 1. Stack y activación del tema

El proyecto corre en `net10.0-windows`. `PresentationFramework.Fluent` viene incluido — **sin NuGet adicional**.

### App.xaml
```xml
<Application.Resources>
  <ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
      <ResourceDictionary Source="pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.xaml"/>
      <ResourceDictionary Source="Resources/Colors.xaml"/>
      <ResourceDictionary Source="Resources/Styles.xaml"/>
    </ResourceDictionary.MergedDictionaries>
  </ResourceDictionary>
</Application.Resources>
```

### App.xaml.cs — selección de tema en caliente
```csharp
Current.ThemeMode = PreferenciasUsuario.GetTema() switch
{
    "Light" => ThemeMode.Light,
    "Dark"  => ThemeMode.Dark,
    _        => ThemeMode.System
};
```
`ThemeMode` es API de primera clase en WPF .NET 10. El cambio es inmediato, sin reinicio. No requiere Mica backdrop.

---

## 2. Estructura de MainWindow

### Jerarquía visual
```
Window (Background="Transparent")
└── Grid root (Background=ApplicationBackgroundBrush)
    ├── Row 0: Title Bar (44px, sin Background — hereda)
    │   ├── Col 0: Botón Atrás (ToolbarButton, IsHitTestVisibleInChrome)
    │   ├── Col 1: Título de la app
    │   └── Col 2: Caption buttons XAML (min/max/close, IsHitTestVisibleInChrome)
    └── Row 1: Grid principal
        ├── Col 0 (220px): Sidebar (DockPanel, sin Background — hereda)
        │   ├── TreeView de navegación (ítems con ícono + texto)
        │   └── Botón Configuración (DockPanel.Dock="Bottom", ToolbarButton)
        └── Col 1 (*): Border elevado (Margin="4,0,0,0",
                                       Background=LayerFillColorDefaultBrush,
                                       BorderBrush=CardStrokeColorDefaultBrush,
                                       BorderThickness="1,1,0,0",
                                       CornerRadius="8,0,0,0")
            └── RootContentFrame (NavigationUIVisibility="Hidden")
        + Grid.RowSpan="2": DialogOverlay (Panel.ZIndex=999, Collapsed por defecto)
```

**Regla de colores del layout (patrón WPFGallery):**
- Root Grid → `{DynamicResource ApplicationBackgroundBrush}` (color base, más oscuro)
- Sidebar + Title bar → **sin Background** (heredan del Root Grid)
- Área de contenido → `{DynamicResource LayerFillColorDefaultBrush}` (color elevado/claro), con borde sutil `CardStrokeColorDefaultBrush` y `CornerRadius="8,0,0,0"`

El contenido se ve como una "tarjeta elevada" sobre el fondo base. El CornerRadius queda visible porque el borde superior izquierdo del Border revela el color base del Root Grid.

### WindowChrome — configuración crítica
```xml
<WindowChrome.WindowChrome>
    <WindowChrome CaptionHeight="44" ResizeBorderThickness="5"
                  GlassFrameThickness="0" UseAeroCaptionButtons="False" />
</WindowChrome.WindowChrome>
```

**Por qué `UseAeroCaptionButtons="False"` + `GlassFrameThickness="0"`:** con el Root Grid pintado de un color opaco (`ApplicationBackgroundBrush`), los caption buttons nativos de DWM quedan tapados y no se ven. La solución estable es deshabilitar tanto los nativos como la extensión de glass frame, y dibujar los botones en XAML con `WindowChrome.IsHitTestVisibleInChrome="True"`. Si en el futuro se activa Mica/transparencia (Window Background transparente), se puede volver a `UseAeroCaptionButtons="True"` y eliminar los XAML.

### Caption buttons en XAML
```xml
<StackPanel Grid.Column="2" Orientation="Horizontal" VerticalAlignment="Top"
            WindowChrome.IsHitTestVisibleInChrome="True">
    <Button Style="{StaticResource CaptionButton}" Click="MinimizeButton_Click">
        <TextBlock FontFamily="Segoe Fluent Icons" FontSize="10" Text="&#xE921;" />
    </Button>
    <Button Style="{StaticResource CaptionButton}" Click="MaximizeButton_Click">
        <TextBlock FontFamily="Segoe Fluent Icons" FontSize="10">
            <TextBlock.Style>
                <Style TargetType="TextBlock">
                    <Setter Property="Text" Value="&#xE922;" />
                    <Style.Triggers>
                        <DataTrigger Binding="{Binding WindowState,
                            RelativeSource={RelativeSource AncestorType=Window}}"
                            Value="Maximized">
                            <Setter Property="Text" Value="&#xE923;" />
                        </DataTrigger>
                    </Style.Triggers>
                </Style>
            </TextBlock.Style>
        </TextBlock>
    </Button>
    <Button Style="{StaticResource CaptionCloseButton}" Click="CloseButton_Click">
        <TextBlock FontFamily="Segoe Fluent Icons" FontSize="10" Text="&#xE8BB;" />
    </Button>
</StackPanel>
```

### Code-behind (MainWindow.xaml.cs)
```csharp
private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    => SystemCommands.MinimizeWindow(this);

private void MaximizeButton_Click(object sender, RoutedEventArgs e)
{
    if (WindowState == WindowState.Maximized)
        SystemCommands.RestoreWindow(this);
    else
        SystemCommands.MaximizeWindow(this);
}

private void CloseButton_Click(object sender, RoutedEventArgs e)
    => SystemCommands.CloseWindow(this);
```

---

## 3. Tokens de color Fluent — uso correcto

### Regla fundamental
- **`{DynamicResource}`** para TODO color del tema → responde a light/dark en caliente
- **`{StaticResource}`** para tokens de layout fijos (CornerRadius, tamaños) → no cambian con el tema

### Tokens de uso frecuente

| Token | Uso |
|-------|-----|
| `TextFillColorPrimaryBrush` | Texto principal, títulos |
| `TextFillColorSecondaryBrush` | Labels de formulario, texto secundario |
| `TextFillColorDisabledBrush` | Controles deshabilitados |
| `TextFillColorInverseBrush` | Texto sobre fondos de color (acento) |
| `LayerFillColorDefaultBrush` | Área de contenido (elevado), cards/panels |
| `ApplicationBackgroundBrush` | Fondo base de la ventana (root Grid, sidebar, title bar) |
| `CardBackgroundFillColorDefaultBrush` | Fondo de diálogos, tarjetas |
| `CardStrokeColorDefaultBrush` | Borde del área de contenido, diálogos, tarjetas |
| `SmokeFillColorDefaultBrush` | Overlay semitransparente de diálogos |
| `ControlAltFillColorTertiaryBrush` | Hover en ToolbarButton |
| `ControlAltFillColorQuarternaryBrush` | Pressed en ToolbarButton |
| `SubtleFillColorSecondaryBrush` | Hover en CaptionButton |
| `SubtleFillColorTertiaryBrush` | Pressed en CaptionButton |
| `SystemFillColorAttentionBackgroundBrush` | Status bar informativa (adapta a tema) |
| `AccentFillColorDefaultBrush` | Controles accent (usar `AccentButtonStyle` en buttons) |
| `ControlCornerRadius` | CornerRadius estándar de controles |

### Lo que NO usar
- `BackgroundColor`, `SurfaceColor`, `BorderColor`, `TextPrimaryColor`, `TextSecondaryColor` — estos estuvieron en Colors.xaml pero se eliminaron por duplicar tokens Fluent
- Colores hexadecimales en XAML (excepto `EstadoReciboToBrushConverter` que los necesita en C# por limitación técnica)
- `AccentLightBrush` para status bars → usar `SystemFillColorAttentionBackgroundBrush`

---

## 4. Resources/Colors.xaml — contenido actual

Colors.xaml contiene **únicamente** los colores de dominio que el tema Fluent no provee:

```xml
<!-- Colores de marca por app (AccentBrush se usa en MainWindow title bar como fallback) -->
<Color x:Key="AccentColor">#1565C0</Color>              <!-- o #00695C para CentroMaritimo -->
<Color x:Key="AccentLightColor">#E3F2FD</Color>         <!-- o #E0F2F1 -->
<SolidColorBrush x:Key="AccentBrush" Color="{StaticResource AccentColor}" />
<SolidColorBrush x:Key="AccentLightBrush" Color="{StaticResource AccentLightColor}" />

<!-- Estados de recibo (colores de dominio, solo usados en EstadoReciboToBrushConverter) -->
<SolidColorBrush x:Key="EstadoEmitidoBrush">#E3F2FD</SolidColorBrush>
<SolidColorBrush x:Key="EstadoEnviadoBrush">#FFF9C4</SolidColorBrush>
<SolidColorBrush x:Key="EstadoPagadoBrush">#E8F5E9</SolidColorBrush>
<SolidColorBrush x:Key="EstadoVencidoBrush">#FFEBEE</SolidColorBrush>
<SolidColorBrush x:Key="EstadoAnuladoBrush">#F5F5F5</SolidColorBrush>
```

---

## 5. Resources/Styles.xaml — estilos definidos

| Clave | Tipo base | Uso |
|-------|-----------|-----|
| `PageTitle` | TextBlock | Título de cada página (20pt SemiBold) |
| `PrimaryButton` | AccentButtonStyle | Acción primaria (Guardar, Emitir, Confirmar) |
| `SecondaryButton` | {x:Type Button} | Acción secundaria (Nuevo, Anular, Examinar) |
| `ToolbarButton` | Propio (borderless) | Botones de toolbar y sidebar (hover sutil) |
| `CaptionButton` | Propio | Minimize, Maximize (hover neutro `SubtleFillColor`) |
| `CaptionCloseButton` | Propio | Close (hover rojo `#C42B1C`, texto blanco) |

**Tamaños de caption buttons:** `Width="46"`, `Height="32"` — iguala la estética nativa de Windows.

---

## 6. Iconos — Segoe Fluent Icons

Usar **siempre Segoe Fluent Icons** (Windows 11+). NO Segoe MDL2 Assets.

```xml
<TextBlock FontFamily="Segoe Fluent Icons" FontSize="15" Text="&#xE80F;" />
```

### Glyphs usados en el proyecto

| Glyph | Código | Uso |
|-------|--------|-----|
| ← Atrás | `&#xE72B;` | Botón back en title bar |
| ⚙ Configuración | `&#xE713;` | Botón settings en sidebar |
| 🏠 Inicio | `&#xE80F;` | Nav: Dashboard |
| 📄 Recibos | `&#xE8A5;` | Nav: Recibos |
| ➤ Emisión | `&#xE8D7;` | Nav: Emisión masiva / Emitir |
| 🏢 Empresas | `&#xE716;` | Nav: Empresas / Agencias |
| 👥 Grupos | `&#xE902;` | Nav: Grupos |
| 🏷 Vouchers | `&#xE722;` | Nav: Vouchers (CentroMaritimo) |
| 📅 Cierre | `&#xE787;` | Nav: Cierre de período (CentroMaritimo) |
| 🚢 Barcos | `&#xEC64;` | Nav: Barcos (CentroMaritimo) |
| _ Minimizar | `&#xE921;` | Caption button |
| □ Maximizar | `&#xE922;` | Caption button |
| ❐ Restaurar | `&#xE923;` | Caption button (cuando maximizado) |
| ✕ Cerrar | `&#xE8BB;` | Caption button close |

---

## 7. Estructura de páginas

Las páginas son `Page` (no `UserControl`) para ser navegadas por `Frame`. Cada página tiene:
- Grid raíz con `Margin="24"`
- Row 0: título `<TextBlock Style="{StaticResource PageTitle}" />`
- Row 1: banner de status (Collapsed por defecto) con `SystemFillColorAttentionBackgroundBrush`
- Rows siguientes: contenido

```xml
<Page ...>
    <Grid Margin="24">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />   <!-- PageTitle -->
            <RowDefinition Height="Auto" />   <!-- Status banner -->
            <RowDefinition Height="Auto" />   <!-- Filtros/toolbar (si aplica) -->
            <RowDefinition Height="*" />      <!-- Contenido principal -->
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Text="..." Style="{StaticResource PageTitle}" />

        <Border Grid.Row="1" Margin="0,0,0,12" Padding="12,8"
                CornerRadius="{StaticResource ControlCornerRadius}"
                Background="{DynamicResource SystemFillColorAttentionBackgroundBrush}"
                Visibility="{Binding HasStatus, Converter={StaticResource BoolToVisibilityConverter}}">
            <TextBlock Text="{Binding StatusMessage}" TextWrapping="Wrap" />
        </Border>

        <!-- ... -->
    </Grid>
</Page>
```

---

## 8. NavigationItem — modelo de ítem de nav

```csharp
public class NavigationItem
{
    public string Title { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = string.Empty;  // glyph Segoe Fluent Icons
    public bool HasIcon => !string.IsNullOrEmpty(IconGlyph);
    public Type? PageType { get; set; }
    public ObservableCollection<NavigationItem> Items { get; set; } = [];
}
```

Los `NavigationItems` se construyen en `MainWindowViewModel.BuildNavigationItems()`. Los glyphs son literales Unicode (no escape sequences en C#) — usar UTF-8 en el archivo fuente.

---

## 9. Diálogos modales

Los diálogos se implementan como `UserControl` inyectados en el `DialogOverlay` de `MainWindow`. Ver implementación en:
- `CamaraPortuaria.UI/Dialogs/` — ConfirmDialog, AlertDialog, InputDialog
- `CamaraPortuaria.UI/Services/DialogService.cs` — implementación de `IDialogService`

Tokens Fluent usados en diálogos:
- Fondo: `CardBackgroundFillColorDefaultBrush`
- Borde: `CardStrokeColorDefaultBrush`
- Texto secundario: `TextFillColorSecondaryBrush`
- Botón primario: `Style="{DynamicResource AccentButtonStyle}"`
- Botón cancelar: sin estilo explícito (usa el default del tema)

---

## 10. Trampa de encoding — IMPORTANTE

Los archivos XAML del proyecto son **UTF-8 sin BOM**. Al modificarlos desde scripts:

```python
# ✓ CORRECTO — Python con encoding explícito
with open(path, 'r', encoding='utf-8-sig') as f:
    content = f.read()
with open(path, 'w', encoding='utf-8') as f:
    f.write(new_content)
```

```powershell
# ✗ PELIGROSO — Get-Content sin -Encoding en Windows 11
# Lee UTF-8 como Windows-1252, luego Set-Content -Encoding UTF8 dobla el encoding
# Resultado: "Ã³" en lugar de "ó", "Ã¡" en lugar de "á", etc.
(Get-Content $file -Raw) -replace ... | Set-Content $file -Encoding UTF8

# ✓ PowerShell correcto
(Get-Content $file -Encoding UTF8 -Raw) -replace ... | Set-Content $file -Encoding UTF8NoBOM
```

Las herramientas Read/Write/Edit de Claude Code son seguras. El problema ocurre solo al usar Bash/PowerShell con `Get-Content` sin encoding explícito.

---

## 11. Trampas del shell Fluent + WindowChrome — lecciones aprendidas

Estas son trampas no obvias que se descubrieron iterando. Si estás tocando `MainWindow.xaml` o el `WindowChrome`, leelas antes para no repetir el ciclo de prueba-error.

### 11.1 Inversión de la jerarquía de elevación (panel "elevado")

**Síntoma:** poner `CornerRadius="8,0,0,0"` en el Border del contenido **no se ve** o se ve "raro" (un panel claro de fondo asomando por la esquina).

**Causa raíz:** Fluent tiene dos tokens de fondo principales y es fácil confundir cuál va arriba:
- `ApplicationBackgroundBrush` → **color base** (más oscuro en Light, base de la ventana)
- `LayerFillColorDefaultBrush` → **color elevado** (más claro en Light, para "tarjetas" que flotan sobre el base)

La intuición de "sidebar más clarito = elevado" es **incorrecta** en el patrón de WPFGallery. El correcto:

| Elemento | Background | Por qué |
|----------|------------|---------|
| Root Grid | `ApplicationBackgroundBrush` | Es el fondo base de la ventana |
| Sidebar + Title bar | **sin Background** (heredan) | Son parte del fondo base, no flotan |
| Content Border | `LayerFillColorDefaultBrush` + Border 1px + CornerRadius | Es la "tarjeta elevada" |

El Border del contenido lleva además:
- `BorderBrush="{DynamicResource CardStrokeColorDefaultBrush}"`
- `BorderThickness="1,1,0,0"` (solo top y left, el right/bottom dan a los bordes de la ventana)
- `Margin="4,0,0,0"` (deja respirar el borde izquierdo separándolo del sidebar)

**Referencia canónica:** `C:\Users\kevin\source\repos\WPF-Samples\Sample Applications\WPFGallery\MainWindow.xaml`. Cuando dudes sobre cómo se compone el shell Fluent, ese repo es la fuente de verdad de Microsoft.

### 11.2 Caption buttons "duplicados" — `GlassFrameThickness="-1"` es la trampa

**Síntoma:** los botones minimizar/maximizar/cerrar XAML se ven "duplicados" o con un halo / glitch superpuesto.

**Causa raíz:** `GlassFrameThickness="-1"` extiende el frame de DWM (glass/Mica) hacia el área client. Aunque pongas `UseAeroCaptionButtons="False"`, DWM puede seguir dibujando los caption buttons nativos como parte del frame extendido, por debajo del XAML. Resultado: dos juegos.

**Combinación correcta (PuertoBB, Background opaco):**
```xml
<WindowChrome CaptionHeight="44" ResizeBorderThickness="5"
              GlassFrameThickness="0" UseAeroCaptionButtons="False" />
```
- `GlassFrameThickness="0"` → no extiende el glass; DWM no intenta dibujar nada en el client area
- `UseAeroCaptionButtons="False"` → nativos deshabilitados
- Caption buttons como `<Button>` XAML con `WindowChrome.IsHitTestVisibleInChrome="True"`

**Síntoma opuesto:** los nativos **no aparecen** si pasas a `UseAeroCaptionButtons="True"` sin eliminar el Background opaco del Root Grid (el Background los tapa).

**Regla rápida:**
- ¿Background opaco en Root Grid? → caption buttons XAML + `GlassFrameThickness="0"` + `UseAeroCaptionButtons="False"`
- ¿Querés Mica/transparencia? → Background del Window/Grid transparente + `GlassFrameThickness="-1"` + `UseAeroCaptionButtons="True"` + **sin** caption buttons XAML

No mezcles "Background opaco + Mica + ambos juegos de botones" — es la receta del bug.

### 11.3 No asumir desde un solo cambio

Cada cambio en el shell (`MainWindow.xaml`, `WindowChrome`, jerarquía de fondos) tiene **efectos visuales que solo se ven ejecutando la app**. Tests automatizados y `dotnet build` pasan aunque la UI esté rota.

Después de tocar el shell:
1. `dotnet build` (debe quedar 0 warnings/errores)
2. Ejecutar ambas apps (CamaraPortuaria + CentroMaritimo) — comparten el mismo shell pero el bug se puede ver solo en una
3. Verificar visualmente: caption buttons (un solo juego, hover funciona, close en rojo), CornerRadius visible, sidebar y contenido con la jerarquía correcta
4. Si hay cambio de tema disponible, alternar Light/Dark y verificar que los tokens DynamicResource responden bien
