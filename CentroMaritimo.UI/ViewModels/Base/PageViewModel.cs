namespace CentroMaritimo.UI.ViewModels.Base;

/// <summary>Base para ViewModels de página: estado de carga (IsBusy) y barra de mensajes.</summary>
public abstract class PageViewModel : BaseViewModel
{
    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set => SetField(ref _isBusy, value); }

    private string? _statusMessage;
    public string? StatusMessage { get => _statusMessage; set { SetField(ref _statusMessage, value); OnPropertyChanged(nameof(HasStatus)); } }

    private bool _isError;
    public bool IsError { get => _isError; set => SetField(ref _isError, value); }

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    protected void MostrarError(string mensaje) { IsError = true; StatusMessage = mensaje; }
    protected void MostrarExito(string mensaje) { IsError = false; StatusMessage = mensaje; }
    protected void LimpiarStatus() { StatusMessage = null; IsError = false; }
}
