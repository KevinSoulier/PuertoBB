using System.Collections.ObjectModel;
using System.Windows.Input;
using CamaraPortuaria.UI.ViewModels.Base;
using CamaraPortuaria.UI.ViewModels.Items;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Services;

namespace CamaraPortuaria.UI.ViewModels;

public class GruposViewModel : PageViewModel
{
    private readonly IGrupoFacturacionRepository _repo;
    private readonly IEmpresaRepository _empresasRepo;
    private readonly IDialogService _dialog;
    private int _editId;
    private List<MiembroGrupoItem> _todosMiembros = [];

    public ObservableCollection<GrupoFacturacion> Grupos { get; } = [];
    public ObservableCollection<MiembroGrupoItem> MiembrosFiltrados { get; } = [];

    private GrupoFacturacion? _seleccionado;
    public GrupoFacturacion? Seleccionado
    {
        get => _seleccionado;
        set { if (SetField(ref _seleccionado, value) && value is not null) _ = CargarEdicionAsync(value.Id); }
    }

    public string NombreEdit { get; set; } = string.Empty;
    public string DescripcionEdit { get; set; } = string.Empty;
    public decimal ImporteEdit { get; set; }

    private string _filtroMiembros = string.Empty;
    public string FiltroMiembros
    {
        get => _filtroMiembros;
        set { if (SetField(ref _filtroMiembros, value)) AplicarFiltroMiembros(); }
    }

    public ICommand NuevoCommand { get; }
    public ICommand GuardarCommand { get; }
    public ICommand EliminarCommand { get; }

    public GruposViewModel(IGrupoFacturacionRepository repo, IEmpresaRepository empresasRepo, IDialogService dialog)
    {
        _repo = repo;
        _empresasRepo = empresasRepo;
        _dialog = dialog;
        NuevoCommand = new AsyncRelayCommand(NuevoAsync);
        GuardarCommand = new AsyncRelayCommand(GuardarAsync);
        EliminarCommand = new AsyncRelayCommand(EliminarAsync, () => Seleccionado is not null);
        _ = CargarListaAsync();
    }

    private async Task CargarListaAsync()
    {
        Grupos.Clear();
        foreach (var g in await _repo.GetActivosAsync()) Grupos.Add(g);
    }

    private async Task NuevoAsync()
    {
        _editId = 0;
        NombreEdit = DescripcionEdit = string.Empty;
        ImporteEdit = 0;
        _seleccionado = null;
        FiltroMiembros = string.Empty;
        await CargarMiembrosAsync(new HashSet<int>());
        NotificarEdicion();
        LimpiarStatus();
    }

    private async Task CargarEdicionAsync(int id)
    {
        var g = await _repo.GetConMiembrosAsync(id);
        if (g is null) return;
        _editId = g.Id;
        NombreEdit = g.Nombre;
        DescripcionEdit = g.Descripcion ?? string.Empty;
        ImporteEdit = g.Importe;
        FiltroMiembros = string.Empty;
        await CargarMiembrosAsync(g.Empresas.Select(e => e.EmpresaId).ToHashSet());
        NotificarEdicion();
    }

    private async Task CargarMiembrosAsync(HashSet<int> miembrosIds)
    {
        _todosMiembros = (await _empresasRepo.GetAllAsync())
            .OrderBy(e => e.Nombre)
            .Select(e => new MiembroGrupoItem(e, miembrosIds.Contains(e.Id)))
            .ToList();
        AplicarFiltroMiembros();
    }

    private void AplicarFiltroMiembros()
    {
        MiembrosFiltrados.Clear();
        var texto = _filtroMiembros.Trim();
        var lista = string.IsNullOrEmpty(texto)
            ? _todosMiembros
            : _todosMiembros.Where(m => m.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase));
        foreach (var m in lista) MiembrosFiltrados.Add(m);
    }

    private async Task GuardarAsync()
    {
        if (string.IsNullOrWhiteSpace(NombreEdit)) { MostrarError("El nombre es obligatorio."); return; }
        if (ImporteEdit <= 0) { MostrarError("El importe debe ser mayor a cero."); return; }

        var seleccionados = _todosMiembros.Where(m => m.EsMiembro).Select(m => m.EmpresaId).ToHashSet();
        try
        {
            if (_editId == 0)
            {
                await _repo.AddAsync(new GrupoFacturacion
                {
                    Nombre = NombreEdit.Trim(),
                    Descripcion = string.IsNullOrWhiteSpace(DescripcionEdit) ? null : DescripcionEdit.Trim(),
                    Importe = ImporteEdit,
                    Activo = true,
                    Empresas = seleccionados.Select(id => new EmpresaGrupo { EmpresaId = id }).ToList()
                });
                MostrarExito("Grupo creado.");
            }
            else
            {
                var existente = await _repo.GetConMiembrosAsync(_editId);
                if (existente is null) { MostrarError("El grupo ya no existe."); return; }
                existente.Nombre = NombreEdit.Trim();
                existente.Descripcion = string.IsNullOrWhiteSpace(DescripcionEdit) ? null : DescripcionEdit.Trim();
                existente.Importe = ImporteEdit;
                existente.Activo = true;
                existente.Empresas.Where(eg => !seleccionados.Contains(eg.EmpresaId)).ToList()
                    .ForEach(eg => existente.Empresas.Remove(eg));
                var actuales = existente.Empresas.Select(eg => eg.EmpresaId).ToHashSet();
                foreach (var id in seleccionados.Where(id => !actuales.Contains(id)))
                    existente.Empresas.Add(new EmpresaGrupo { EmpresaId = id, GrupoFacturacionId = existente.Id });
                await _repo.UpdateAsync(existente);
                MostrarExito("Grupo actualizado.");
            }
            await CargarListaAsync();
        }
        catch (Exception ex)
        {
            MostrarError($"No se pudo guardar: {ex.Message}");
        }
    }

    private async Task EliminarAsync()
    {
        if (Seleccionado is null) return;
        if (!await _dialog.ShowConfirmAsync("Eliminar grupo",
                $"¿Eliminar el grupo «{Seleccionado.Nombre}»?", "Eliminar", "Cancelar")) return;
        try
        {
            await _repo.DeleteAsync(Seleccionado.Id);
            MostrarExito("Grupo eliminado.");
            await NuevoAsync();
            await CargarListaAsync();
        }
        catch (Exception ex)
        {
            MostrarError($"No se pudo eliminar (¿tiene recibos asociados?): {ex.Message}");
        }
    }

    private void NotificarEdicion()
    {
        OnPropertyChanged(nameof(NombreEdit));
        OnPropertyChanged(nameof(DescripcionEdit));
        OnPropertyChanged(nameof(ImporteEdit));
    }
}
