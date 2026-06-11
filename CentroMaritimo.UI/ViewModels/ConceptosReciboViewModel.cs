using System.Collections.ObjectModel;
using System.Windows.Input;
using CentroMaritimo.UI.ViewModels.Base;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;

namespace CentroMaritimo.UI.ViewModels;

/// <summary>CRUD del catálogo de detalles/conceptos reutilizables de los recibos.</summary>
public class ConceptosReciboViewModel : PageViewModel
{
    private readonly IConceptoReciboRepository _repo;
    private readonly IDialogService _dialog;
    private int _editId;
    private List<ConceptoRecibo> _todos = [];
    private string _snapNombre = string.Empty;

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
        set
        {
            if (SetField(ref _seleccionado, value) && value is not null)
            {
                _editId = value.Id;
                NombreEdit = value.Nombre;
                OnPropertyChanged(nameof(NombreEdit));
            }
        }
    }

    public string NombreEdit { get; set; } = string.Empty;

    private bool _enEdicion;
    public bool EnEdicion
    {
        get => _enEdicion;
        private set { if (SetField(ref _enEdicion, value)) { OnPropertyChanged(nameof(NoEnEdicion)); CommandManager.InvalidateRequerySuggested(); } }
    }
    public bool NoEnEdicion => !EnEdicion;

    public ICommand NuevoCommand { get; }
    public ICommand EditarCommand { get; }
    public ICommand AceptarCommand { get; }
    public ICommand CancelarCommand { get; }
    public ICommand EliminarCommand { get; }

    public ConceptosReciboViewModel(IConceptoReciboRepository repo, IDialogService dialog)
    {
        _repo = repo;
        _dialog = dialog;
        NuevoCommand = new RelayCommand(_ => Nuevo(), _ => !EnEdicion);
        EditarCommand = new RelayCommand(_ => Editar(), _ => Seleccionado is not null && !EnEdicion);
        AceptarCommand = new AsyncRelayCommand(AceptarAsync, () => EnEdicion);
        CancelarCommand = new RelayCommand(_ => Cancelar(), _ => EnEdicion);
        EliminarCommand = new AsyncRelayCommand(EliminarAsync, () => Seleccionado is not null && !EnEdicion);
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

    private void Nuevo()
    {
        _editId = 0;
        NombreEdit = string.Empty;
        _seleccionado = null;
        OnPropertyChanged(nameof(Seleccionado));
        OnPropertyChanged(nameof(NombreEdit));
        _snapNombre = string.Empty;
        EnEdicion = true;
        LimpiarStatus();
    }

    private void Editar()
    {
        _snapNombre = NombreEdit;
        EnEdicion = true;
    }

    private void Cancelar()
    {
        if (_editId == 0)
        {
            NombreEdit = string.Empty;
            _seleccionado = null;
            OnPropertyChanged(nameof(Seleccionado));
        }
        else
        {
            NombreEdit = _snapNombre;
        }
        OnPropertyChanged(nameof(NombreEdit));
        EnEdicion = false;
        LimpiarStatus();
    }

    private async Task AceptarAsync()
    {
        if (string.IsNullOrWhiteSpace(NombreEdit)) { MostrarError("El detalle es obligatorio."); return; }
        try
        {
            if (_editId == 0)
            {
                if (await _repo.GetPorNombreAsync(NombreEdit.Trim()) is not null) { MostrarError("Ya existe un detalle con ese nombre."); return; }
                var nuevo = new ConceptoRecibo { Nombre = NombreEdit.Trim() };
                await _repo.AddAsync(nuevo);
                _editId = nuevo.Id;
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
            var idGuardado = _editId;
            await CargarAsync();
            var guardado = ConceptosFiltrados.FirstOrDefault(c => c.Id == idGuardado)
                           ?? _todos.FirstOrDefault(c => c.Id == idGuardado);
            if (guardado is not null) { _seleccionado = guardado; OnPropertyChanged(nameof(Seleccionado)); }
            EnEdicion = false;
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
            _editId = 0;
            NombreEdit = string.Empty;
            _seleccionado = null;
            OnPropertyChanged(nameof(Seleccionado));
            OnPropertyChanged(nameof(NombreEdit));
            EnEdicion = false;
            await CargarAsync();
        }
        catch (Exception ex) { MostrarError($"No se pudo eliminar: {ex.Message}"); }
    }
}
