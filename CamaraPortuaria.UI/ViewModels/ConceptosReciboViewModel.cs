using System.Collections.ObjectModel;
using System.Windows.Input;
using CamaraPortuaria.UI.ViewModels.Base;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Services;

namespace CamaraPortuaria.UI.ViewModels;

/// <summary>CRUD del catálogo de detalles/conceptos reutilizables de los recibos.</summary>
public class ConceptosReciboViewModel : PageViewModel
{
    private readonly IConceptoReciboRepository _repo;
    private readonly IDialogService _dialog;
    private int _editId;
    private List<ConceptoRecibo> _todos = [];

    public ObservableCollection<ConceptoRecibo> ConceptosFiltrados { get; } = [];

    private string _filtro = string.Empty;
    public string Filtro
    {
        get => _filtro;
        set { if (SetField(ref _filtro, value)) AplicarFiltro(); }
    }

    private ConceptoRecibo? _seleccionado;
    public ConceptoRecibo? Seleccionado
    {
        get => _seleccionado;
        set { if (SetField(ref _seleccionado, value) && value is not null) { _editId = value.Id; NombreEdit = value.Nombre; OnPropertyChanged(nameof(NombreEdit)); } }
    }

    public string NombreEdit { get; set; } = string.Empty;

    public ICommand NuevoCommand { get; }
    public ICommand GuardarCommand { get; }
    public ICommand EliminarCommand { get; }

    public ConceptosReciboViewModel(IConceptoReciboRepository repo, IDialogService dialog)
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
        _todos = (await _repo.GetAllAsync()).OrderBy(c => c.Nombre).ToList();
        AplicarFiltro();
    }

    private void AplicarFiltro()
    {
        ConceptosFiltrados.Clear();
        var texto = _filtro.Trim();
        var lista = string.IsNullOrEmpty(texto)
            ? _todos
            : _todos.Where(c => c.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase));
        foreach (var c in lista) ConceptosFiltrados.Add(c);
    }

    private async Task GuardarAsync()
    {
        if (string.IsNullOrWhiteSpace(NombreEdit)) { MostrarError("El detalle es obligatorio."); return; }
        try
        {
            if (_editId == 0)
            {
                if (await _repo.GetPorNombreAsync(NombreEdit.Trim()) is not null) { MostrarError("Ya existe un detalle con ese nombre."); return; }
                await _repo.AddAsync(new ConceptoRecibo { Nombre = NombreEdit.Trim() });
                MostrarExito("Detalle creado.");
            }
            else
            {
                var existente = await _repo.GetByIdAsync(_editId);
                if (existente is null) { MostrarError("El detalle ya no existe."); return; }
                existente.Nombre = NombreEdit.Trim();
                await _repo.UpdateAsync(existente);
                MostrarExito("Detalle actualizado.");
            }
            await CargarAsync();
        }
        catch (Exception ex) { MostrarError($"No se pudo guardar: {ex.Message}"); }
    }

    private async Task EliminarAsync()
    {
        if (Seleccionado is null) return;
        if (!await _dialog.ShowConfirmAsync("Eliminar detalle", $"¿Eliminar «{Seleccionado.Nombre}»?", "Eliminar", "Cancelar")) return;
        try
        {
            await _repo.DeleteAsync(Seleccionado.Id);
            MostrarExito("Detalle eliminado.");
            await CargarAsync();
        }
        catch (Exception ex) { MostrarError($"No se pudo eliminar: {ex.Message}"); }
    }
}
