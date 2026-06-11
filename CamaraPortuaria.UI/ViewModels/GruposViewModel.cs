using System.Collections.ObjectModel;
using System.Windows.Input;
using CamaraPortuaria.UI.ViewModels.Base;
using CamaraPortuaria.UI.ViewModels.Items;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Services.Common;

namespace CamaraPortuaria.UI.ViewModels;

public class GruposViewModel : PageViewModel
{
    private readonly IGrupoFacturacionRepository _repo;
    private readonly IEmpresaRepository _empresasRepo;
    private readonly IDialogService _dialog;
    private int _editId;
    private List<MiembroGrupoItem> _todosMiembros = [];

    public ObservableCollection<GrupoFacturacion> Grupos { get; } = [];
    public ObservableCollection<MiembroGrupoItem> MiembrosDelGrupo { get; } = [];
    public ObservableCollection<MiembroGrupoItem> EmpresasDisponibles { get; } = [];

    /// <summary>Ítems a facturar a cada miembro del grupo (multi-ítem).</summary>
    public ObservableCollection<LineaEmisionItem> LineasEdit { get; } = [];

    private GrupoFacturacion? _seleccionado;
    public GrupoFacturacion? Seleccionado
    {
        get => _seleccionado;
        set { if (SetField(ref _seleccionado, value) && value is not null) _ = MostrarAsync(value.Id); }
    }

    public string NombreEdit { get; set; } = string.Empty;
    public string DescripcionEdit { get; set; } = string.Empty;

    private bool _enEdicion;
    public bool EnEdicion
    {
        get => _enEdicion;
        private set { if (SetField(ref _enEdicion, value)) { OnPropertyChanged(nameof(NoEnEdicion)); CommandManager.InvalidateRequerySuggested(); } }
    }
    public bool NoEnEdicion => !EnEdicion;

    private string _descripcionLinea = string.Empty;
    public string DescripcionLinea { get => _descripcionLinea; set => SetField(ref _descripcionLinea, value); }

    private decimal _cantidadLinea = 1;
    public decimal CantidadLinea { get => _cantidadLinea; set => SetField(ref _cantidadLinea, value); }

    private decimal _precioUnitarioLinea;
    public decimal PrecioUnitarioLinea { get => _precioUnitarioLinea; set => SetField(ref _precioUnitarioLinea, value); }

    public decimal TotalEdit => LineasEdit.Sum(l => l.Importe);
    public string TotalEditTexto => Formato.Moneda(TotalEdit);

    private string _filtroMiembros = string.Empty;
    public string FiltroMiembros
    {
        get => _filtroMiembros;
        set { if (SetField(ref _filtroMiembros, value)) RefrescarMiembros(); }
    }

    private MiembroGrupoItem? _empresaSeleccionada;
    public MiembroGrupoItem? EmpresaSeleccionada
    {
        get => _empresaSeleccionada;
        set { if (SetField(ref _empresaSeleccionada, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public ICommand NuevoCommand { get; }
    public ICommand EditarCommand { get; }
    public ICommand AceptarCommand { get; }
    public ICommand CancelarCommand { get; }
    public ICommand EliminarCommand { get; }
    public ICommand AgregarLineaCommand { get; }
    public ICommand QuitarLineaCommand { get; }
    public ICommand AgregarMiembroCommand { get; }
    public ICommand QuitarMiembroCommand { get; }

    public GruposViewModel(IGrupoFacturacionRepository repo, IEmpresaRepository empresasRepo, IDialogService dialog)
    {
        _repo = repo;
        _empresasRepo = empresasRepo;
        _dialog = dialog;
        NuevoCommand = new AsyncRelayCommand(NuevoAsync, () => !EnEdicion);
        EditarCommand = new AsyncRelayCommand(EditarAsync, () => Seleccionado is not null && !EnEdicion);
        AceptarCommand = new AsyncRelayCommand(AceptarAsync,
            () => EnEdicion && !string.IsNullOrWhiteSpace(NombreEdit) && LineasEdit.Count > 0);
        CancelarCommand = new AsyncRelayCommand(CancelarAsync, () => EnEdicion);
        EliminarCommand = new AsyncRelayCommand(EliminarAsync, () => Seleccionado is not null && !EnEdicion);
        AgregarLineaCommand = new RelayCommand(_ => AgregarLinea(),
            _ => EnEdicion && !string.IsNullOrWhiteSpace(DescripcionLinea) && CantidadLinea > 0 && PrecioUnitarioLinea > 0);
        QuitarLineaCommand = new RelayCommand(param => { if (param is LineaEmisionItem l) { LineasEdit.Remove(l); RefrescarTotal(); } });
        AgregarMiembroCommand = new RelayCommand(_ => AgregarMiembro(), _ => EmpresaSeleccionada != null && EnEdicion);
        QuitarMiembroCommand = new RelayCommand(param => { if (param is MiembroGrupoItem m) QuitarMiembro(m); });
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
        LineasEdit.Clear();
        LimpiarCargaLinea();
        RefrescarTotal();
        _seleccionado = null;
        OnPropertyChanged(nameof(Seleccionado));
        FiltroMiembros = string.Empty;
        await CargarMiembrosAsync(new HashSet<int>());
        EnEdicion = true;
        NotificarEdicion();
        LimpiarStatus();
    }

    private Task EditarAsync() => CargarGrupoAsync(_editId, enEdicion: true);

    private async Task CancelarAsync()
    {
        if (_editId > 0)
        {
            await MostrarAsync(_editId);
        }
        else
        {
            _editId = 0;
            NombreEdit = DescripcionEdit = string.Empty;
            LineasEdit.Clear();
            LimpiarCargaLinea();
            RefrescarTotal();
            _seleccionado = null;
            OnPropertyChanged(nameof(Seleccionado));
            FiltroMiembros = string.Empty;
            _todosMiembros = [];
            LimpiarMiembros();
            EnEdicion = false;
            NotificarEdicion();
            LimpiarStatus();
        }
    }

    private Task MostrarAsync(int id) => CargarGrupoAsync(id, enEdicion: false);

    private async Task CargarGrupoAsync(int id, bool enEdicion)
    {
        var g = await _repo.GetConMiembrosAsync(id);
        if (g is null) return;
        _editId = g.Id;
        NombreEdit = g.Nombre;
        DescripcionEdit = g.Descripcion ?? string.Empty;
        LineasEdit.Clear();
        foreach (var l in g.Lineas.OrderBy(l => l.Orden))
            LineasEdit.Add(new LineaEmisionItem(l.Descripcion, l.Cantidad, l.PrecioUnitario));
        LimpiarCargaLinea();
        RefrescarTotal();
        FiltroMiembros = string.Empty;
        await CargarMiembrosAsync(g.Empresas.Select(e => e.EmpresaId).ToHashSet());
        EnEdicion = enEdicion;
        NotificarEdicion();
    }

    private void AgregarLinea()
    {
        var desc = DescripcionLinea.Trim();
        if (string.IsNullOrWhiteSpace(desc)) { MostrarError("Ingrese una descripción para el ítem."); return; }
        if (CantidadLinea <= 0) { MostrarError("La cantidad debe ser mayor a cero."); return; }
        if (PrecioUnitarioLinea <= 0) { MostrarError("El precio unitario debe ser mayor a cero."); return; }

        LineasEdit.Add(new LineaEmisionItem(desc, CantidadLinea, PrecioUnitarioLinea));
        LimpiarCargaLinea();
        RefrescarTotal();
    }

    private void LimpiarCargaLinea()
    {
        DescripcionLinea = string.Empty;
        CantidadLinea = 1;
        PrecioUnitarioLinea = 0;
    }

    private void RefrescarTotal()
    {
        OnPropertyChanged(nameof(TotalEdit));
        OnPropertyChanged(nameof(TotalEditTexto));
        CommandManager.InvalidateRequerySuggested();
    }

    private async Task CargarMiembrosAsync(HashSet<int> miembrosIds)
    {
        _todosMiembros = (await _empresasRepo.GetAllAsync())
            .OrderBy(e => e.Nombre)
            .Select(e => new MiembroGrupoItem(e, miembrosIds.Contains(e.Id)))
            .ToList();
        RefrescarMiembros();
    }

    private void RefrescarMiembros()
    {
        // El ComboBox de miembros es editable: al seleccionar un ítem, además de fijar
        // SelectedItem (EmpresaSeleccionada) WPF copia su Text → FiltroMiembros. Si reconstruimos
        // la lista en ese momento, el Clear() saca de EmpresasDisponibles al ítem seleccionado y
        // el ComboBox pone SelectedItem=null, perdiendo la selección (el botón Agregar nunca se
        // habilita). Si el filtro coincide exactamente con el Nombre del ítem ya seleccionado,
        // el cambio viene de la selección (no de tipear): no reconstruimos, para preservarla.
        if (_empresaSeleccionada != null && _filtroMiembros.Trim() == _empresaSeleccionada.Nombre)
            return;

        MiembrosDelGrupo.Clear();
        EmpresasDisponibles.Clear();
        var filtro = _filtroMiembros.Trim();
        foreach (var m in _todosMiembros)
        {
            if (m.EsMiembro)
                MiembrosDelGrupo.Add(m);
            else if (string.IsNullOrEmpty(filtro) || m.Nombre.Contains(filtro, StringComparison.OrdinalIgnoreCase))
                EmpresasDisponibles.Add(m);
        }
    }

    private void LimpiarMiembros()
    {
        MiembrosDelGrupo.Clear();
        EmpresasDisponibles.Clear();
        EmpresaSeleccionada = null;
    }

    private void AgregarMiembro()
    {
        if (EmpresaSeleccionada == null) return;
        EmpresaSeleccionada.EsMiembro = true;
        EmpresaSeleccionada = null;
        FiltroMiembros = string.Empty;
        RefrescarMiembros();
    }

    private void QuitarMiembro(MiembroGrupoItem item)
    {
        item.EsMiembro = false;
        RefrescarMiembros();
    }

    private async Task AceptarAsync()
    {
        if (string.IsNullOrWhiteSpace(NombreEdit)) { MostrarError("El nombre es obligatorio."); return; }
        if (LineasEdit.Count == 0) { MostrarError("Agregue al menos un ítem al grupo."); return; }

        var seleccionados = _todosMiembros.Where(m => m.EsMiembro).Select(m => m.EmpresaId).ToHashSet();
        var total = TotalEdit;
        try
        {
            if (_editId == 0)
            {
                var nuevo = new GrupoFacturacion
                {
                    Nombre = NombreEdit.Trim(),
                    Descripcion = string.IsNullOrWhiteSpace(DescripcionEdit) ? null : DescripcionEdit.Trim(),
                    Importe = total,
                    Activo = true,
                    Lineas = ConstruirLineas(),
                    Empresas = seleccionados.Select(id => new EmpresaGrupo { EmpresaId = id }).ToList()
                };
                await _repo.AddAsync(nuevo);
                _editId = nuevo.Id;
                MostrarExito("Grupo creado.");
            }
            else
            {
                var existente = await _repo.GetConMiembrosAsync(_editId);
                if (existente is null) { MostrarError("El grupo ya no existe."); return; }
                existente.Nombre = NombreEdit.Trim();
                existente.Descripcion = string.IsNullOrWhiteSpace(DescripcionEdit) ? null : DescripcionEdit.Trim();
                existente.Importe = total;
                existente.Activo = true;
                existente.Lineas.Clear();
                foreach (var l in ConstruirLineas()) existente.Lineas.Add(l);
                existente.Empresas.Where(eg => !seleccionados.Contains(eg.EmpresaId)).ToList()
                    .ForEach(eg => existente.Empresas.Remove(eg));
                var actuales = existente.Empresas.Select(eg => eg.EmpresaId).ToHashSet();
                foreach (var id in seleccionados.Where(id => !actuales.Contains(id)))
                    existente.Empresas.Add(new EmpresaGrupo { EmpresaId = id, GrupoFacturacionId = existente.Id });
                await _repo.UpdateAsync(existente);
                MostrarExito("Grupo actualizado.");
            }
            var idGuardado = _editId;
            await CargarListaAsync();
            var guardado = Grupos.FirstOrDefault(g => g.Id == idGuardado);
            if (guardado is not null) { _seleccionado = guardado; OnPropertyChanged(nameof(Seleccionado)); }
            EnEdicion = false;
        }
        catch (Exception ex)
        {
            MostrarError($"No se pudo guardar: {ex.Message}");
        }
    }

    private List<GrupoFacturacionLinea> ConstruirLineas()
        => LineasEdit.Select((l, i) => new GrupoFacturacionLinea
        {
            Descripcion = l.Descripcion,
            Cantidad = l.Cantidad,
            PrecioUnitario = l.PrecioUnitario,
            Importe = l.Importe,
            Orden = i
        }).ToList();

    private async Task EliminarAsync()
    {
        if (Seleccionado is null) return;
        if (!await _dialog.ShowConfirmAsync("Eliminar grupo",
                $"¿Eliminar el grupo «{Seleccionado.Nombre}»?", "Eliminar", "Cancelar")) return;
        try
        {
            await _repo.DeleteAsync(Seleccionado.Id);
            MostrarExito("Grupo eliminado.");
            _editId = 0;
            NombreEdit = DescripcionEdit = string.Empty;
            LineasEdit.Clear();
            RefrescarTotal();
            _todosMiembros = [];
            LimpiarMiembros();
            _seleccionado = null;
            OnPropertyChanged(nameof(Seleccionado));
            EnEdicion = false;
            NotificarEdicion();
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
    }
}
