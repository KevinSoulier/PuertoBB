using System.Collections.ObjectModel;
using System.Windows.Input;
using CentroMaritimo.UI.ViewModels.Base;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;

namespace CentroMaritimo.UI.ViewModels;

public class BarcosViewModel : PageViewModel
{
    private readonly IBarcoRepository _repo;
    private readonly IDialogService _dialog;
    private int _editId;
    private List<Barco> _todosLosBarcos = [];

    public ObservableCollection<Barco> BarcosFiltrados { get; } = [];

    private string _filtro = string.Empty;
    public string Filtro
    {
        get => _filtro;
        set { if (SetField(ref _filtro, value)) AplicarFiltro(); }
    }

    private Barco? _seleccionado;
    public Barco? Seleccionado
    {
        get => _seleccionado;
        set { if (SetField(ref _seleccionado, value) && value is not null) { _editId = value.Id; NombreEdit = value.Nombre; OnPropertyChanged(nameof(NombreEdit)); } }
    }

    public string NombreEdit { get; set; } = string.Empty;

    public ICommand NuevoCommand { get; }
    public ICommand GuardarCommand { get; }
    public ICommand EliminarCommand { get; }

    public BarcosViewModel(IBarcoRepository repo, IDialogService dialog)
    {
        _repo = repo;
        _dialog = dialog;
        NuevoCommand = new RelayCommand(_ => { _editId = 0; NombreEdit = string.Empty; _seleccionado = null; OnPropertyChanged(nameof(NombreEdit)); LimpiarStatus(); });
        GuardarCommand = new AsyncRelayCommand(GuardarAsync);
        EliminarCommand = new AsyncRelayCommand(EliminarAsync, () => Seleccionado is not null);
        _ = CargarAsync();
    }

    private async Task CargarAsync()
    {
        _todosLosBarcos = (await _repo.GetAllAsync()).OrderBy(b => b.Nombre).ToList();
        AplicarFiltro();
    }

    private void AplicarFiltro()
    {
        BarcosFiltrados.Clear();
        var texto = _filtro.Trim();
        var lista = string.IsNullOrEmpty(texto)
            ? _todosLosBarcos
            : _todosLosBarcos.Where(b => b.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase));
        foreach (var b in lista) BarcosFiltrados.Add(b);
    }

    private async Task GuardarAsync()
    {
        if (string.IsNullOrWhiteSpace(NombreEdit)) { MostrarError("El nombre es obligatorio."); return; }
        try
        {
            if (_editId == 0)
            {
                if (await _repo.GetPorNombreAsync(NombreEdit.Trim()) is not null) { MostrarError("Ya existe un barco con ese nombre."); return; }
                await _repo.AddAsync(new Barco { Nombre = NombreEdit.Trim() });
                MostrarExito("Barco creado.");
            }
            else
            {
                var existente = await _repo.GetByIdAsync(_editId);
                if (existente is null) { MostrarError("El barco ya no existe."); return; }
                existente.Nombre = NombreEdit.Trim();
                await _repo.UpdateAsync(existente);
                MostrarExito("Barco actualizado.");
            }
            await CargarAsync();
        }
        catch (Exception ex) { MostrarError($"No se pudo guardar: {ex.Message}"); }
    }

    private async Task EliminarAsync()
    {
        if (Seleccionado is null) return;
        if (!await _dialog.ShowConfirmAsync("Eliminar barco", $"¿Eliminar «{Seleccionado.Nombre}»?", "Eliminar", "Cancelar")) return;
        try
        {
            await _repo.DeleteAsync(Seleccionado.Id);
            MostrarExito("Barco eliminado.");
            await CargarAsync();
        }
        catch (Exception ex) { MostrarError($"No se pudo eliminar (¿tiene vouchers?): {ex.Message}"); }
    }
}
