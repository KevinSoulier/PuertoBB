# Fluent Design con WPF-UI — guía y mejores prácticas

> Stack de UI **vigente** de PuertoBB. Reemplaza al enfoque de `fluent-navigation.md`
> (Fluent nativo + `TreeView` + `ThemeMode`), que queda como referencia histórica.
> Extraído del repo de referencia `wpfui` (librería **WPF-UI 4.3.0** de lepo.co) y de su
> demo MVVM (`samples/Wpf.Ui.Demo.Mvvm`), que usa el mismo stack que PuertoBB (Generic Host + DI).

## 1. Paquetes y arranque

Ambas apps (`CamaraPortuaria.UI`, `CentroMaritimo.UI`, `net10.0-windows`) referencian:

```xml
<PackageReference Include="WPF-UI" Version="4.3.0" />
<PackageReference Include="WPF-UI.DependencyInjection" Version="4.3.0" />
```

`App.xaml` carga el tema y los controles de WPF-UI (los `Colors.xaml`/`Styles.xaml` propios
y los converters se mantienen):

```xml
xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
...
<ui:ThemesDictionary Theme="Light" />
<ui:ControlsDictionary />
<ResourceDictionary Source="Resources/Colors.xaml" />
<ResourceDictionary Source="Resources/Styles.xaml" />
```

> WPF-UI usa **los mismos resource keys WinUI** que el Fluent nativo
> (`TextFillColorPrimaryBrush`, `ApplicationBackgroundBrush`, `CardBackgroundFillColorDefaultBrush`,
> `ControlAltFillColor*`, `SubtleFillColor*`, `SystemFillColor*`, `SmokeFillColorDefaultBrush`, …).
> Por eso las páginas existentes no necesitaron cambiar sus `DynamicResource`.

## 2. Integración con DI (Generic Host)

Patrón de la demo MVVM, replicado en `App.xaml.cs`:

```csharp
services.AddNavigationViewPageProvider();                    // Wpf.Ui.DependencyInjection
services.AddSingleton<INavigationService, NavigationService>(); // Wpf.Ui
services.AddSingleton<MainWindow>();
// Páginas/ViewModels: transient (el page provider las resuelve por GetService).
```

- `AddNavigationViewPageProvider()` registra un `INavigationViewPageProvider` que resuelve las
  páginas (`TargetPageType`) desde el `IServiceProvider`.
- `NavigationService` (de WPF-UI) recibe ese provider y, al hacer
  `SetNavigationControl(RootNavigation)`, conecta el `NavigationView` con el contenedor DI.

## 3. Ventana: `FluentWindow` + Mica + `TitleBar`

```xml
<ui:FluentWindow ... ExtendsContentIntoTitleBar="True"
                 WindowBackdropType="Mica" WindowCornerPreference="Round"
                 Foreground="{DynamicResource TextFillColorPrimaryBrush}">
  <Grid>
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto" /><RowDefinition Height="*" />
    </Grid.RowDefinitions>
    <!-- Back en la esquina superior izquierda + TitleBar a la derecha -->
    <Grid Grid.Row="0">
      <Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
      <Button x:Name="BackButton" Grid.Column="0" Style="{StaticResource ToolbarButton}"
              Width="40" Height="32" Margin="8,0,0,0" VerticalAlignment="Center"
              IsEnabled="False" Click="BackButton_Click">
        <ui:SymbolIcon Symbol="ArrowLeft24" FontSize="16" />
      </Button>
      <ui:TitleBar Grid.Column="1" Title="…">
        <ui:TitleBar.Icon><ui:SymbolIcon Symbol="Building24" /></ui:TitleBar.Icon>
      </ui:TitleBar>
    </Grid>
    <ui:NavigationView Grid.Row="1" IsBackButtonVisible="Collapsed" ... />
  </Grid>
</ui:FluentWindow>
```

Reglas de la transparencia (Mica):
- `ExtendsContentIntoTitleBar="True"` es **obligatorio** para el backdrop (lo valida el propio control).
- **No** poner `Background` opaco en la ventana ni en el `Grid` raíz: Mica necesita transparencia
  (el error histórico de PuertoBB era pintar `ApplicationBackgroundBrush` y tapar el efecto).
- `FluentWindow` aplica el efecto en `OnSourceInitialized` vía DWM. **Mica** requiere Windows 11;
  en Windows 10 degrada a opaco sin romper. (`Acrylic` funciona desde Win7; `Tabbed` es Win11 22H2+.)
- `ui:TitleBar` aporta arrastre de ventana y botones minimizar/maximizar/cerrar; se eliminan los
  caption buttons y el `WindowChrome` hechos a mano.
- **Color de texto — dos piezas obligatorias** (WPF-UI sobre-escribe `FontSize`/`FontWeight` de
  `TextBlock` vía `TextBlockMetadata`, pero **no** el `Foreground`, así que el texto plano cae al negro
  del sistema: "muy oscuro" en claro, ilegible en oscuro):
  1. `Foreground="{DynamicResource TextFillColorPrimaryBrush}"` en el **`FluentWindow`** → cubre el texto
     fuera del contenido navegado (barra, overlays).
  2. Fijar el `Foreground` del tema **por página**, en el evento `Navigated` del `NavigationView`
     (un solo lugar cubre todas las páginas):
     ```csharp
     RootNavigation.Navigated += (_, e) =>
     {
         if (e.Page is Page page)
             page.SetResourceReference(Page.ForegroundProperty, "TextFillColorPrimaryBrush");
     };
     ```
     **Por qué hace falta además:** el contenido del `NavigationView` se hospeda en
     `NavigationViewContentPresenter`, que **deriva de `Frame`** — y `Frame` es **frontera de herencia**
     en WPF: el `Foreground` del `FluentWindow` **no cruza** a las páginas. Fijarlo por `Page` se hereda a
     sus `TextBlock` (sin pisar el `Foreground` propio de cada control) y con `SetResourceReference` sigue
     el cambio de tema. ⚠️ Un `Style TargetType="Page"` implícito **no sirve**: los estilos implícitos solo
     aplican al **tipo exacto**, no a las subclases (`DashboardPage`, `ConfiguracionPage`, …).

### Back en la barra de ventana (no en el sidebar)

El back del `NavigationView` (`IsBackButtonVisible`) vive **sobre el panel lateral**. Para ponerlo en la
**esquina superior izquierda de la ventana**, se colapsa (`IsBackButtonVisible="Collapsed"`) y se usa un
`Button` propio a la izquierda del `ui:TitleBar`. Cableado en el code-behind (con `CaptionHeight=0` que
fija `FluentWindow`, el botón es clickable sin `WindowChrome.IsHitTestVisibleInChrome`):

```csharp
RootNavigation.Navigated += (_, _) => BackButton.IsEnabled = RootNavigation.CanGoBack;
private void BackButton_Click(object s, RoutedEventArgs e) => RootNavigation.GoBack();
```

## 4. Navegación lateral: `NavigationView`

```xml
<ui:NavigationView x:Name="RootNavigation"
                   PaneDisplayMode="Left"
                   IsBackButtonVisible="Collapsed"
                   MenuItemsSource="{Binding NavigationItems}"
                   FooterMenuItemsSource="{Binding NavigationFooter}" />
```

Los items se arman en el `MainWindowViewModel` como `ObservableCollection<object>` de
`NavigationViewItem` (ctor `name, SymbolRegular, targetPageType`):

```csharp
public ObservableCollection<object> NavigationItems { get; } =
[
    new NavigationViewItem("Inicio", SymbolRegular.Home24, typeof(DashboardPage)),
    new NavigationViewItem("Recibos", SymbolRegular.ReceiptMoney24, typeof(RecibosPage)),
    // …
];
public ObservableCollection<object> NavigationFooter { get; } =
[
    new NavigationViewItem("Configuración", SymbolRegular.Settings24, typeof(ConfiguracionPage)),
];
```

- El `NavigationView` hospeda su propio frame de contenido y el indicador de selección animado.
- El back se **colapsa** acá y se reubica en la barra de ventana (ver §3 "Back en la barra de ventana").
- `PaneDisplayMode`: `Left` (expandible con toggle), `LeftMinimal`, `LeftFluent`, `Top`, `Bottom`.
- Páginas: siguen siendo `Page` con el ViewModel inyectado por constructor (sin cambios).

## 5. Iconos: `SymbolIcon` (Fluent System Icons)

- WPF-UI embebe `FluentSystemIcons-Regular.ttf` / `-Filled.ttf` (~9.200 glyphs) y los expone tipados
  con el enum `SymbolRegular` (y `SymbolFilled`). Uso: `<ui:SymbolIcon Symbol="Home24" />`.
- Reemplaza los glyphs Unicode crudos de `Segoe Fluent Icons` (que en PuertoBB estaban **vacíos**).

### Catálogo de iconos por página (PuertoBB)

| Página | App | `SymbolRegular` |
|---|---|---|
| Inicio / Dashboard | ambas | `Home24` |
| Recibos | ambas | `ReceiptMoney24` |
| Emisión masiva | ambas | `DocumentMultiple24` |
| Empresas | CP | `BuildingMultiple24` |
| Grupos | ambas | `PeopleTeam24` |
| Vouchers | CM | `TicketDiagonal24` |
| Cierre de período | CM | `CalendarCheckmark24` |
| Agencias | CM | `BuildingShop24` |
| Barcos | CM | `VehicleShip24` |
| Configuración (footer) | ambas | `Settings24` |
| Icono de la ventana | CP / CM | `Building24` / `VehicleShip24` |

## 6. Tema y acento

- Cambio de tema vía `ApplicationThemeManager.Apply(ApplicationTheme.Light/Dark, WindowBackdropType.Mica)`
  o `ApplicationThemeManager.ApplySystemTheme()`. Persistencia en `tema.txt` (`PreferenciasUsuario`, D-10).
- Modo **System** en vivo: `SystemThemeWatcher.Watch(window, WindowBackdropType.Mica)`; al elegir
  Light/Dark se hace `SystemThemeWatcher.UnWatch(window)`.
- **El acento es el del sistema.** No se fuerza ningún color: se deja `updateAccent: true` (el default de
  `Apply`/`ApplySystemTheme`/`Watch`), así WPF-UI toma el **color de acento de Windows**. Los botones de
  acento lo siguen porque `AccentButtonBackground` → `AccentFillColorDefault` (DynamicResource), igual que
  el indicador de selección del `NavigationView`. El `AccentColor` de marca de `Colors.xaml`
  (CP `#1565C0`, CM `#00695C`) queda disponible para realces puntuales, pero **no** se usa como acento global.

## 7. Botones de acento

WPF-UI **no** define `AccentButtonStyle` (sí lo hacía el Fluent nativo). El estilo propio
`Resources/Styles.xaml` lo replica con un template sobre los brushes `AccentButton*` de WPF-UI, de modo
que `PrimaryButton` y los diálogos sigan funcionando sin cambiar el XAML de páginas. Alternativa nativa de
WPF-UI: `<ui:Button Appearance="Primary" />`.

## 8. Diálogos

Se mantiene el sistema por overlay (`Dialogs/ConfirmDialog|AlertDialog|InputDialog` + `DialogOverlay` +
`IDialogService` con `TaskCompletionSource`, D-09): funciona con los brushes del tema WPF-UI. Migrar a
`ui:ContentDialog` queda como posible mejora futura.

## 9. Mapa de archivos (patrón repetido por app)

- `*/*.csproj` — paquetes WPF-UI; sin `EnableMicaBackdrop` ni `NoWarn WPF0001`.
- `*/App.xaml` — `ThemesDictionary` + `ControlsDictionary`.
- `*/App.xaml.cs` — `AddNavigationViewPageProvider`, `INavigationService` de WPF-UI, `ApplicationThemeManager`, acento de marca.
- `*/MainWindow.xaml(.cs)` — `FluentWindow` + `TitleBar` + `NavigationView`; `SetNavigationControl`, `SystemThemeWatcher`.
- `*/ViewModels/MainWindowViewModel.cs` — items `NavigationViewItem` + `SymbolIcon`.
- `*/Resources/Styles.xaml` — `AccentButtonStyle` propio.
- `*/Views/ConfiguracionPage.xaml.cs` — cambio de tema con `ApplicationThemeManager`/`SystemThemeWatcher`.
