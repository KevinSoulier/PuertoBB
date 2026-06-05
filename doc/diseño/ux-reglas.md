# Reglas de UX y diseño WPF

## Layout general

- Sidebar de navegación de **220px** a la izquierda con color acento de la entidad
- Área de contenido principal a la derecha con `ContentControl` o `Frame`
- Fuente: **Segoe UI** en toda la app. Sin fuentes externas.

## Márgenes

| Contexto | Valor |
| --- | --- |
| Exterior de página | 24px |
| Entre secciones | 16px |
| Entre elementos relacionados | 8px |

## Validación

- Validación en **tiempo real** con mensajes bajo el campo, nunca en popup
- Los mensajes de error de validación van debajo del control que los originó

## Estados de carga

- Overlay `ProgressBar` o `ProgressRing` durante operaciones asíncronas
- **No bloquear la UI.** Usar binding a una propiedad `IsBusy` en el ViewModel.

## Mensajes de resultado

- Mensajes de error/éxito en **barra superior de la vista**, no en dialogs
- Confirmaciones destructivas (eliminar, anular) **siempre en dialog modal**

## Prohibiciones

- **Nunca `MessageBox` nativo.** Siempre via `IDialogService` inyectado.
- Sin lógica de negocio en code-behind. Solo `InitializeComponent()`.
- Sin fuentes externas (Google Fonts, etc.) en XAML/WPF.

## Fuentes en PDF (QuestPDF)

La regla "sin fuentes externas" aplica exclusivamente a XAML/WPF. Los PDFs generados con QuestPDF **pueden** usar fuentes embebidas.

- WPF: **Segoe UI** siempre, sin excepción
- PDF con QuestPDF: actualmente usa **Lato** embebida — aceptable. Si se desea cambiar, Segoe UI también es válida en QuestPDF.
