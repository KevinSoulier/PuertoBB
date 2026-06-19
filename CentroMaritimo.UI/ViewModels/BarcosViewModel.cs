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
    private string _snapNombre = string.Empty;

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

    public BarcosViewModel(IBarcoRepository repo, IDialogService dialog)
    {
        _repo = repo;
        _dialog = dialog;
        NuevoCommand = new RelayCommand(_ => Nuevo(), _ => !EnEdicion);
        EditarCommand = new RelayCommand(_ => Editar(), _ => Seleccionado is not null && !EnEdicion);
        AceptarCommand = new AsyncRelayCommand(AceptarAsync, () => EnEdicion);
        CancelarCommand = new RelayCommand(_ => Cancelar(), _ => EnEdicion);
        EliminarCommand = new AsyncRelayCommand(EliminarAsync, () => Seleccionado is not null && !EnEdicion);
        CargarSeguro(CargarAsync);
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
        if (string.IsNullOrWhiteSpace(NombreEdit)) { MostrarError("El nombre es obligatorio."); return; }
        try
        {
            if (_editId == 0)
            {
                if (await _repo.GetPorNombreAsync(NombreEdit.Trim()) is not null) { MostrarError("Ya existe un barco con ese nombre."); return; }
                var nuevo = new Barco { Nombre = NombreEdit.Trim() };
                await _repo.AddAsync(nuevo);
                _editId = nuevo.Id;
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
            var idGuardado = _editId;
            await CargarAsync();
            var guardado = BarcosFiltrados.FirstOrDefault(b => b.Id == idGuardado)
                           ?? _todosLosBarcos.FirstOrDefault(b => b.Id == idGuardado);
            if (guardado is not null) { _seleccionado = guardado; OnPropertyChanged(nameof(Seleccionado)); }
            EnEdicion = false;
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
            _editId = 0;
            NombreEdit = string.Empty;
            _seleccionado = null;
            OnPropertyChanged(nameof(Seleccionado));
            OnPropertyChanged(nameof(NombreEdit));
            EnEdicion = false;
            await CargarAsync();
        }
        catch (Exception ex) { MostrarError($"No se pudo eliminar (¿tiene vouchers?): {ex.Message}"); }
    }
}
