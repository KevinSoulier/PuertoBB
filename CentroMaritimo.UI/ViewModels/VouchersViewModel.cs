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
    private readonly IClienteRepository _agenciasRepo;
    private readonly IBarcoRepository _barcosRepo;
    private readonly IConfiguracionRepository _configRepo;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;
    private readonly ICentroMaritimoPdfService _pdf;

    // Listas fuentes (no filtradas)
    private List<Barco> _todosBarcos = [];
    private List<Cliente> _todasClientes = [];

    public ObservableCollection<VoucherItem> Vouchers { get; private set; } = [];
    public ObservableCollection<Barco> BarcosFiltrados { get; } = [];
    public ObservableCollection<Cliente> ClientesFiltradas { get; } = [];

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
        set { if (SetField(ref _mesIndex, value)) { _mes = value + 1; CargarSeguro(BuscarAsync); } }
    }

    private int _mes = DateTime.Today.Month;
    private int _anio = DateTime.Today.Year;
    public int Anio { get => _anio; set { if (SetField(ref _anio, value)) CargarSeguro(BuscarAsync); } }

    // Lista fuente sin filtrar + texto de búsqueda sobre la grilla
    private List<VoucherItem> _todosVouchers = [];

    private string _textoBusqueda = string.Empty;
    public string TextoBusqueda
    {
        get => _textoBusqueda;
        set { if (SetField(ref _textoBusqueda, value)) AplicarFiltro(); }
    }

    // Selección en grilla
    private VoucherItem? _seleccionado;
    public VoucherItem? Seleccionado { get => _seleccionado; set => SetField(ref _seleccionado, value); }

    // Autocomplete agencia
    private string _agenciaTexto = string.Empty;
    public string ClienteTexto
    {
        get => _agenciaTexto;
        set
        {
            if (SetField(ref _agenciaTexto, value))
            {
                if (_agenciaSeleccionada?.Nombre != value) _agenciaSeleccionada = null;
                FiltrarClientes();
            }
        }
    }

    private Cliente? _agenciaSeleccionada;
    public Cliente? ClienteSeleccionada
    {
        get => _agenciaSeleccionada;
        set
        {
            if (SetField(ref _agenciaSeleccionada, value) && value is not null)
            {
                _agenciaTexto = value.Nombre;
                OnPropertyChanged(nameof(ClienteTexto));
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
    public ICommand IrAClientesCommand { get; }
    public ICommand DescargarVoucherCommand { get; }
    public ICommand PrevisualizarVoucherCommand { get; }

    public VouchersViewModel(
        IVoucherService service,
        IVoucherRepository repo,
        IClienteRepository agenciasRepo,
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
        IrAClientesCommand = new RelayCommand(_ => _nav.Navigate(typeof(ClientesPage)));
        DescargarVoucherCommand    = new AsyncRelayCommand(DescargarVoucherAsync,    () => Seleccionado is not null);
        PrevisualizarVoucherCommand = new AsyncRelayCommand(PrevisualizarVoucherAsync, () => Seleccionado is not null);

        CargarSeguro(InicializarAsync);
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

    // Nombre de archivo consistente para descarga y guardado desde el visor: "Nro - Cliente - Barco".
    private static string NombreArchivoVoucher(Voucher voucher)
        => Formato.NombreArchivoSeguro($"{voucher.Numero} - {voucher.Cliente?.Nombre} - {voucher.Barco?.Nombre}");

    private async Task InicializarAsync()
    {
        _todasClientes = (await _agenciasRepo.GetTodasConEmailsAsync()).OrderBy(a => a.Nombre).ToList();
        _todosBarcos = (await _barcosRepo.GetAllAsync()).OrderBy(b => b.Nombre).ToList();
        FiltrarClientes();
        FiltrarBarcos();

        var config = await _configRepo.GetAsync();
        if (config.ImporteVoucherPredeterminado > 0)
            ImporteNuevo = config.ImporteVoucherPredeterminado;

        await BuscarAsync();
    }

    private void FiltrarClientes()
    {
        ClientesFiltradas.Clear();
        var texto = _agenciaTexto.Trim();
        var lista = string.IsNullOrEmpty(texto)
            ? _todasClientes
            : _todasClientes.Where(a => a.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase));
        foreach (var a in lista) ClientesFiltradas.Add(a);
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

    private Task BuscarAsync()
    {
        LimpiarStatus();
        return EjecutarOcupadoAsync("Cargando", async () =>
        {
            var res = await _service.GetDelPeriodoAsync(_anio, _mes);
            _todosVouchers = (res.Success && res.Data is not null)
                ? res.Data.Select(v => new VoucherItem(v)).ToList()
                : [];
            AplicarFiltro();
        });
    }

    private void AplicarFiltro()
    {
        var texto = _textoBusqueda.Trim();
        var lista = string.IsNullOrEmpty(texto)
            ? _todosVouchers
            : _todosVouchers.Where(v =>
                v.Numero.ToString().Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                v.Cliente.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                v.Barco.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                v.Fecha.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                v.Importe.Contains(texto, StringComparison.OrdinalIgnoreCase));
        // Reconstruir la colección borra la selección del DataGrid: re-seleccionar por Id
        // para que la toolbar (que opera sobre Seleccionado) sobreviva a recargas y filtrado.
        var seleccionadoId = Seleccionado?.Id;
        Vouchers = new ObservableCollection<VoucherItem>(lista);
        OnPropertyChanged(nameof(Vouchers));
        if (seleccionadoId is int id)
            Seleccionado = Vouchers.FirstOrDefault(v => v.Id == id);
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
            var agenciaEncontrada = _todasClientes.FirstOrDefault(a =>
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
            var agenciaEncontrada = _todasClientes.FirstOrDefault(a =>
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
            ClienteId = _agenciaSeleccionada.Id
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
        ClienteSeleccionada = _todasClientes.FirstOrDefault(a => a.Id == v.ClienteId);
        BarcoSeleccionado = _todosBarcos.FirstOrDefault(b => b.Id == v.BarcoId);
        if (ClienteSeleccionada is null) { ClienteTexto = v.Cliente?.Nombre ?? string.Empty; }
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
        ClienteTexto = string.Empty;
        _agenciaSeleccionada = null;
        OnPropertyChanged(nameof(ClienteSeleccionada));
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
