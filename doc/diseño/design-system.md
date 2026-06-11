# Design System — PuertoBB (CP + CM)

> Fuente de verdad de la consistencia visual de las dos apps WPF (CamaraPortuaria y CentroMaritimo).
> Regla base: **CP y CM se ven igual**; solo cambia el color de acento. Cualquier control nuevo debe seguir esto.

## Acentos por app

| App | Acento (hex) | Uso |
|---|---|---|
| CamaraPortuaria | `#1565C0` (azul) | Acento de UI + `ColorAcentoHex` del PDF |
| CentroMaritimo | `#00695C` (teal) | Acento de UI + `ColorAcentoHex` del PDF |

Los colores salen de `Resources/Colors.xaml` (UI) y de `PdfTheme` (PDF). **No hardcodear** colores en páginas.

## Botones de operación

- Usar **siempre** el estilo compartido `AccionIconButton` (barra de acciones) o `AccionIconButtonCompact` (acciones por fila).
- Estructura: `Content` (texto en español) + `ui:Button.Icon` con `ui:SymbolIcon` + **`ToolTip` obligatorio**.
- El botón primario de la barra lleva `Appearance="Primary"` (uno por barra).
- Roles: Primary = acción principal (Emitir/Buscar/Cerrar período); default = acciones secundarias; acciones destructivas (Anular/Eliminar) en default con icono claro.

```xml
<ui:Button Content="Marcar pagado" Command="{Binding MarcarPagadoCommand}"
           Style="{StaticResource AccionIconButton}"
           ToolTip="Marcar el recibo seleccionado como pagado">
    <ui:Button.Icon><ui:SymbolIcon Symbol="CheckmarkCircle24" /></ui:Button.Icon>
</ui:Button>
```

## Catálogo canónico operación → icono → tooltip

**Regla de oro: la misma operación usa el mismo `SymbolIcon` en toda la solución (CP y CM).**

| Operación | Símbolo (WPF-UI) | ToolTip (plantilla) |
|---|---|---|
| Buscar | `Search24` | "Buscar … del período seleccionado" |
| Actualizar / Refrescar | `ArrowSync24` | "Actualizar la lista" / "Recargar …" |
| Reintentar | `ArrowSync24` | "Reintentar el CAE o el envío del recibo seleccionado" |
| Nuevo / Agregar | `Add24` | "Crear un nuevo …" / "Agregar …" |
| Guardar | `Save24` | "Guardar …" |
| Editar | `Edit24` | "Editar … seleccionado" |
| Eliminar | `Delete24` | "Eliminar … seleccionado" |
| Cancelar (edición) | `Dismiss24` | "Cancelar la edición en curso" |
| **Emitir** (sin mail) | `Receipt24` | "Emitir el recibo" |
| **Emitir y enviar** | `Send24` | "Emitir … y enviarlos por mail" |
| Reenviar mail | `Mail24` | "Reenviar … por correo" |
| Enviar mails (lote) | `Mail24` | "Enviar el mail con el PDF a todas las …" |
| Anular | `Prohibited24` | "Anular … emitiendo una nota de crédito" |
| **Previsualizar PDF** | `DocumentPdf24` | "Abrir el PDF de … en el visor" |
| Descargar PDF | `ArrowDownload24` | "Guardar el PDF de … en disco" |
| Marcar pagado | `CheckmarkCircle24` | "Marcar … como pagado" |
| Marcar activo (PV) | `Checkmark24` | "Usar este punto de venta para emitir" |
| Cerrar período | `LockClosed24` | "Generar recibos y enviar mails a todas las agencias pendientes" |
| Emitir recibos (lote, sin mail) | `DocumentBulletList24` | "Generar los recibos AFIP sin enviar mails" |
| Generar backup | `ArrowDownload24` | "Guardar una copia de seguridad de la base en disco" |
| Restaurar backup | `ArrowUpload24` | "Reemplazar la base actual por una copia previa" |
| Verificar integridad | `ShieldCheckmark24` | "Comprobar que la base no esté dañada" |
| Compactar (VACUUM) | `Broom24` | "Reconstruir el archivo y recuperar espacio" |
| Optimizar consultas | `Flash24` | "Actualizar estadísticas para mejorar performance" |
| Probar conexión | `PlugConnected24` | "Verificar el servicio/credenciales" |
| Examinar archivo… | `FolderOpen24` (sugerido) | "Seleccionar un archivo del disco" |

> Excepción documentada: los botones `Editar`/`Guardar`/`Cancelar` **por sección** dentro de Configuración pueden ir
> sin ToolTip (texto + contexto ya son claros). Los demás botones de operación llevan ToolTip siempre.

## Patrón de página

1. Título con `Style="{StaticResource PageTitle}"`.
2. Barra de acciones horizontal (filtros + botones `AccionIconButton`).
3. Lista/grid principal (importes alineados a la derecha; estados con color vía converter).
4. Diálogos: **siempre** vía `IDialogService` (overlay Fluent). **Nunca** `MessageBox` (salvo el handler de último recurso en `App.xaml.cs`).
5. Inputs alineados con `MinHeight=36` (ya en estilos).

## Estados de UI (pendiente de homogeneizar — ver informe)

- **Vacío**: cuando una lista no tiene datos, mostrar mensaje + icono tenue (no dejar la grilla muda).
- **Carga**: usar `IsBusy` + indicador; deshabilitar acciones mientras corre.
- **Error**: mensaje claro vía `IDialogService`/snackbar; nunca excepción cruda.

## Formato (es-AR)

- Importes y fechas con cultura es-AR consistente en UI, PDF y mail (símbolo `$`, separador de miles, decimales).
- Centralizar en `Formato` (Services) / helpers de UI; no formatear ad-hoc en cada binding.
