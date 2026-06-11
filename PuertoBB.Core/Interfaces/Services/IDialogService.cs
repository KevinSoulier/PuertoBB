using PuertoBB.Core.Models;

namespace PuertoBB.Core.Interfaces.Services;

/// <summary>
/// Único punto de acceso a diálogos modales desde los ViewModels.
/// Implementado con overlay Fluent en MainWindow — nunca MessageBox nativo.
/// </summary>
public interface IDialogService
{
    /// <summary>Confirmación (típicamente destructiva: eliminar, anular). True si el usuario confirma.</summary>
    Task<bool> ShowConfirmAsync(string title, string message, string confirmText = "Confirmar", string cancelText = "Cancelar");

    /// <summary>Aviso/error/éxito con un solo botón.</summary>
    Task ShowAlertAsync(string title, string message, string closeText = "Aceptar");

    /// <summary>Captura de texto. Devuelve null si se cancela.</summary>
    Task<string?> ShowInputAsync(string title, string placeholder, string? initialValue = null);

    /// <summary>
    /// Previsualiza un PDF en un visor embebido (overlay). Cierra al aceptar.
    /// <paramref name="nombreArchivo"/> (sin extensión) es el nombre sugerido al guardar desde el visor.
    /// </summary>
    Task ShowPdfAsync(byte[] pdfBytes, string titulo, string? nombreArchivo = null);

    /// <summary>Formulario de emisión individual. Devuelve null si el usuario canceló.</summary>
    Task<EmisionIndividualResult?> ShowEmisionIndividualAsync(
        string labelEntidad,
        IReadOnlyList<EntidadEmisionItem> entidades,
        IReadOnlyList<string> conceptos);
}
