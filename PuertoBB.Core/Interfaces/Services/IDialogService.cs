namespace PuertoBB.Core.Interfaces.Services;

public interface IDialogService
{
    Task<bool> ConfirmAsync(string message, string title = "Confirmar");
    Task ShowErrorAsync(string message, string title = "Error");
    Task ShowInfoAsync(string message, string title = "Información");
}
