using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using CentroMaritimo.UI.ViewModels.Base;
using CentroMaritimo.UI.ViewModels.Items;
using CentroMaritimo.UI.Views;
using Microsoft.Win32;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Services.Common;
using Wpf.Ui;

namespace CentroMaritimo.UI.ViewModels;

public class VouchersViewModel : PageViewModel
{
    private static readonly CultureInfo _es = new("es-AR");

    private readonly IVoucherService _service;
    private readonly IVoucherRepository _repo;
    private readonly IAgenciaRepository _agenciasRepo;
    private readonly IBarcoRepository _barcosRepo;
    private readonly IConfiguracionRepository _configRepo;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;
    private readonly ICentroMaritimoPdfService _pdf;

    // Listas fuentes (no filtradas)
    private List<Barco> _todosBarcos = [];
    private List<Agencia> _todasAgencias = [];

    public ObservableCollection<VoucherItem> Vouchers { get; } = [];
    public ObservableCollection<Barco> BarcosFiltrados { get; } = [];
    public ObservableCollection<Agencia> AgenciasFiltradas { get; } = [];

    // Período de búsqueda
    public IReadOnlyList<string> MesesNombres { get; } =
        Enumerable.Range(1, 12)
            .Select(m => _es.DateTimeFormat.GetMonthName(m))
            .Select(n => char.ToUpper(n[0]) + n[1..])
            .ToList();

    private int _mesIndex = DateTime.Today.Month - 1;
    public int MesIndex
    {
        get => _mesIndex;
        set { if (SetField(ref _mesIndex, value)) _mes = value + 1; }
    }

    private int _mes = DateTime.Today.Month;
    private int _anio = DateTime.Today.Year;
    public int Anio { get => _anio; set => SetField(ref _anio, value); }

    // Selección en grilla
    private VoucherItem? _seleccionado;
    public VoucherItem? Seleccionado { get => _seleccionado; set => SetField(ref _seleccionado, value); }

    // Autocomplete agencia
    private string _agenciaTexto = string.Empty;
    public string AgenciaTexto
    {
        get => _agenciaTexto;
        set
        {
            if (SetField(ref _agenciaTexto, value))
            {
                if (_agenciaSeleccionada?.Nombre != value) _agenciaSeleccionada = null;
                FiltrarAgencias();
            }
        }
    }

    private Agencia? _agenciaSeleccionada;
    public Agencia? AgenciaSeleccionada
    {
        get => _agenciaSeleccionada;
        set
        {
            if (SetField(ref _agenciaSeleccionada, value) && value is not null)
            {
                _agenciaTexto = value.Nombre;
                OnPropertyChanged(nameof(AgenciaTexto));
            }
        }
    }

    // Autocomplete barco
    private string _barcoTexto = string.Empty;
    public string BarcoTexto
    {
        get => _barcoTexto;
        set
        {
            if (SetField(ref _barcoTexto, value))
            {
                if (_barcoSeleccionado?.Nombre != value) _barcoSeleccionado = null;
                FiltrarBarcos();
            }
        }
    }

    private Barco? _barcoSeleccionado;
    public Barco? BarcoSeleccionado
    {
        get => _barcoSeleccionado;
        set
        {
            if (SetField(ref _barcoSeleccionado, value) && value is not null)
            {
                _barcoTexto = value.Nombre;
                OnPropertyChanged(nameof(BarcoTexto));
            }
        }
    }

    // Fecha y importe
    private DateTime _fechaNueva = DateTime.Today;
    public DateTime FechaNueva { get => _fechaNueva; set => SetField(ref _fechaNueva, value); }

    private decimal _importeNuevo;
    public decimal ImporteNuevo { get => _importeNuevo; set => SetField(ref _importeNuevo, value); }

    // Modo edición
    private bool _modoEdicion;
    public bool ModoEdicion { get => _modoEdicion; set { SetField(ref _modoEdicion, value); OnPropertyChanged(nameof(TextoBotonGuardar)); } }
    public string TextoBotonGuardar => _modoEdicion ? "Actualizar" : "Crear";

    private int _editandoId;

    public ICommand BuscarCommand { get; }
    public ICommand CrearCommand { get; }
    public ICommand EliminarCommand { get; }
    public ICommand EditarCommand { get; }
    public ICommand CancelarEdicionCommand { get; }
    public ICommand IrAAgenciasCommand { get; }
    public ICommand DescargarVoucherCommand { get; }
    public ICommand PrevisualizarVoucherCommand { get; }

    public VouchersViewModel(
        IVoucherService service,
        IVoucherRepository repo,
        IAgenciaRepository agenciasRepo,
        IBarcoRepository barcosRepo,
        IConfiguracionRepository configRepo,
        IDialogService dialog,
        INavigationService nav,
        ICentroMaritimoPdfService pdf)
    {
        _service = service;
        _repo = repo;
        _agenciasRepo = agenciasRepo;
        _barcosRepo = barcosRepo;
        _configRepo = configRepo;
        _dialog = dialog;
        _nav = nav;
        _pdf = pdf;

        BuscarCommand = new AsyncRelayCommand(BuscarAsync);
        CrearCommand = new AsyncRelayCommand(CrearOActualizarAsync);
        EliminarCommand = new AsyncRelayCommand(EliminarAsync, () => Seleccionado is { Consolidado: false });
        EditarCommand = new AsyncRelayCommand(CargarEdicionAsync, () => Seleccionado is { Consolidado: false });
        CancelarEdicionCommand = new RelayCommand(_ => CancelarEdicion());
        IrAAgenciasCommand = new RelayCommand(_ => _nav.Navigate(typeof(AgenciasPage)));
        DescargarVoucherCommand    = new AsyncRelayCommand(DescargarVoucherAsync,    () => Seleccionado is not null);
        PrevisualizarVoucherCommand = new AsyncRelayCommand(PrevisualizarVoucherAsync, () => Seleccionado is not null);

        _ = InicializarAsync();
    }

    private async Task DescargarVoucherAsync()
    {
        if (Seleccionado is null) return;
        var voucher = await _repo.GetByIdConDetalleAsync(Seleccionado.Id);
        if (voucher is null) { MostrarError("El voucher no existe."); return; }

        var nombre = NombreArchivoVoucher(voucher);
        var dlg = new SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = $"{nombre}.pdf"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var bytes = await _pdf.GenerarPdfVoucherAsync(voucher);
            await File.WriteAllBytesAsync(dlg.FileName, bytes);
            MostrarExito($"PDF generado: {Path.GetFileName(dlg.FileName)}");
        }
        catch (Exception ex) { MostrarError($"No se pudo generar el PDF: {ex.Message}"); }
    }

    private async Task PrevisualizarVoucherAsync()
    {
        if (Seleccionado is null) return;
        var voucher = await _repo.GetByIdConDetalleAsync(Seleccionado.Id);
        if (voucher is null) { MostrarError("El voucher no existe."); return; }

        try
        {
            var bytes = await _pdf.GenerarPdfVoucherAsync(voucher);
            await _dialog.ShowPdfAsync(bytes, $"Voucher N° {voucher.Numero}", NombreArchivoVoucher(voucher));
        }
        catch (Exception ex) { MostrarError($"No se pudo previsualizar: {ex.Message}"); }
    }

    // Nombre de archivo consistente para descarga y guardado desde el visor: "Nro - Agencia - Barco".
    private static string NombreArchivoVoucher(Voucher voucher)
        => Formato.NombreArchivoSeguro($"{voucher.Numero} - {voucher.Agencia?.Nombre} - {voucher.Barco?.Nombre}");

    private async Task InicializarAsync()
    {
        _todasAgencias = (await _agenciasRepo.GetTodasConEmailsAsync()).OrderBy(a => a.Nombre).ToList();
        _todosBarcos = (await _barcosRepo.GetAllAsync()).OrderBy(b => b.Nombre).ToList();
        FiltrarAgencias();
        FiltrarBarcos();

        var config = await _configRepo.GetAsync();
        if (config.ImporteVoucherPredeterminado > 0)
            ImporteNuevo = config.ImporteVoucherPredeterminado;

        await BuscarAsync();
    }

    private void FiltrarAgencias()
    {
        AgenciasFiltradas.Clear();
        var texto = _agenciaTexto.Trim();
        var lista = string.IsNullOrEmpty(texto)
            ? _todasAgencias
            : _todasAgencias.Where(a => a.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase));
        foreach (var a in lista) AgenciasFiltradas.Add(a);
    }

    private void FiltrarBarcos()
    {
        BarcosFiltrados.Clear();
        var texto = _barcoTexto.Trim();
        var lista = string.IsNullOrEmpty(texto)
            ? _todosBarcos
            : _todosBarcos.Where(b => b.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase));
        foreach (var b in lista) BarcosFiltrados.Add(b);
    }

    private async Task BuscarAsync()
    {
        IsBusy = true;
        LimpiarStatus();
        try
        {
            var res = await _service.GetDelPeriodoAsync(_anio, _mes);
            Vouchers.Clear();
            if (res.Success && res.Data is not null)
                foreach (var v in res.Data) Vouchers.Add(new VoucherItem(v));
        }
        finally { IsBusy = false; }
    }

    private async Task CrearOActualizarAsync()
    {
        if (_modoEdicion)
            await ActualizarAsync();
        else
            await CrearAsync();
    }

    private async Task CrearAsync()
    {
        if (_agenciaSeleccionada is null && string.IsNullOrWhiteSpace(_agenciaTexto))
        { MostrarError("Seleccione o escriba una agencia."); return; }
        if (string.IsNullOrWhiteSpace(_barcoTexto))
        { MostrarError("Ingrese el nombre del barco."); return; }
        if (ImporteNuevo <= 0) { MostrarError("El importe debe ser mayor a cero."); return; }

        // Resolver agencia
        if (_agenciaSeleccionada is null)
        {
            var agenciaEncontrada = _todasAgencias.FirstOrDefault(a =>
                a.Nombre.Equals(_agenciaTexto.Trim(), StringComparison.OrdinalIgnoreCase));
            if (agenciaEncontrada is null) { MostrarError("La agencia no existe. Creela en la sección Agencias."); return; }
            _agenciaSeleccionada = agenciaEncontrada;
        }

        // Resolver/crear barco
        var nombreBarco = _barcoTexto.Trim();
        var barcoId = _barcoSeleccionado?.Id ?? 0;
        if (barcoId == 0)
        {
            var existente = _todosBarcos.FirstOrDefault(b =>
                b.Nombre.Equals(nombreBarco, StringComparison.OrdinalIgnoreCase));
            if (existente is not null)
            {
                barcoId = existente.Id;
            }
            else
            {
                // Crear nuevo barco
                var nuevoBarco = new Barco { Nombre = char.ToUpper(nombreBarco[0]) + nombreBarco[1..].ToLower() };
                await _barcosRepo.AddAsync(nuevoBarco);
                _todosBarcos = (await _barcosRepo.GetAllAsync()).OrderBy(b => b.Nombre).ToList();
                FiltrarBarcos();
                barcoId = _todosBarcos.First(b => b.Nombre.Equals(nuevoBarco.Nombre, StringComparison.OrdinalIgnoreCase)).Id;
            }
        }

        var res = await _service.CrearVoucherAsync(_agenciaSeleccionada.Id, barcoId, FechaNueva, ImporteNuevo);
        if (!res.Success) { MostrarError(res.ErrorMessage ?? "No se pudo crear el voucher."); return; }

        MostrarExito($"Voucher Nro. {res.Data!.Numero} creado.");
        LimpiarFormulario();
        await BuscarAsync();
    }

    private async Task ActualizarAsync()
    {
        if (_agenciaSeleccionada is null && string.IsNullOrWhiteSpace(_agenciaTexto))
        { MostrarError("Seleccione o escriba una agencia."); return; }
        if (string.IsNullOrWhiteSpace(_barcoTexto)) { MostrarError("El barco es obligatorio."); return; }
        if (ImporteNuevo <= 0) { MostrarError("El importe debe ser mayor a cero."); return; }

        // Resolver agencia por texto si no quedó seleccionada en el combo
        if (_agenciaSeleccionada is null)
        {
            var agenciaEncontrada = _todasAgencias.FirstOrDefault(a =>
                a.Nombre.Equals(_agenciaTexto.Trim(), StringComparison.OrdinalIgnoreCase));
            if (agenciaEncontrada is null) { MostrarError("La agencia no existe. Creela en la sección Agencias."); return; }
            _agenciaSeleccionada = agenciaEncontrada;
        }

        var barcoId = _barcoSeleccionado?.Id ?? 0;
        if (barcoId == 0)
        {
            var existente = _todosBarcos.FirstOrDefault(b =>
                b.Nombre.Equals(_barcoTexto.Trim(), StringComparison.OrdinalIgnoreCase));
            if (existente is null) { MostrarError("El barco no se encontró."); return; }
            barcoId = existente.Id;
        }

        var voucher = new Voucher
        {
            Id = _editandoId,
            BarcoId = barcoId,
            Importe = ImporteNuevo,
            Fecha = FechaNueva,
            AgenciaId = _agenciaSeleccionada.Id
        };
        var res = await _service.ActualizarVoucherAsync(voucher);
        if (!res.Success) { MostrarError(res.ErrorMessage ?? "No se pudo actualizar."); return; }

        MostrarExito("Voucher actualizado.");
        CancelarEdicion();
        await BuscarAsync();
    }

    private async Task CargarEdicionAsync()
    {
        if (Seleccionado is null) return;
        var v = await _repo.GetByIdAsync(Seleccionado.Id);
        if (v is null) return;

        _editandoId = v.Id;
        AgenciaSeleccionada = _todasAgencias.FirstOrDefault(a => a.Id == v.AgenciaId);
        BarcoSeleccionado = _todosBarcos.FirstOrDefault(b => b.Id == v.BarcoId);
        if (AgenciaSeleccionada is null) { AgenciaTexto = v.Agencia?.Nombre ?? string.Empty; }
        if (BarcoSeleccionado is null) { BarcoTexto = v.Barco?.Nombre ?? string.Empty; }
        FechaNueva = v.Fecha;
        ImporteNuevo = v.Importe;
        ModoEdicion = true;
    }

    private void CancelarEdicion()
    {
        _editandoId = 0;
        ModoEdicion = false;
        LimpiarFormulario();
    }

    private void LimpiarFormulario()
    {
        AgenciaTexto = string.Empty;
        _agenciaSeleccionada = null;
        OnPropertyChanged(nameof(AgenciaSeleccionada));
        BarcoTexto = string.Empty;
        _barcoSeleccionado = null;
        OnPropertyChanged(nameof(BarcoSeleccionado));
        FechaNueva = DateTime.Today;
    }

    private async Task EliminarAsync()
    {
        if (Seleccionado is null) return;
        if (!await _dialog.ShowConfirmAsync("Eliminar voucher",
                $"¿Eliminar el voucher Nro. {Seleccionado.Numero}?", "Eliminar", "Cancelar")) return;
        var res = await _service.EliminarVoucherAsync(Seleccionado.Id);
        if (res.Success) { MostrarExito("Voucher eliminado."); await BuscarAsync(); }
        else MostrarError(res.ErrorMessage ?? "No se pudo eliminar.");
    }
}
