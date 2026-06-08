using CamaraPortuaria.UI.Services;

namespace CamaraPortuaria.UI.ViewModels.Base;

/// <summary>Base para ViewModels de página: estado de carga (IsBusy) y toasts Fluent.</summary>
public abstract class PageViewModel : BaseViewModel
{
    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set => SetField(ref _isBusy, value); }

    protected void MostrarError(string mensaje) => SnackbarHost.Error(mensaje);
    protected void MostrarExito(string mensaje) => SnackbarHost.Exito(mensaje);
    protected void LimpiarStatus() { /* obsoleto: el toaster se autocierra por timeout */ }
}
