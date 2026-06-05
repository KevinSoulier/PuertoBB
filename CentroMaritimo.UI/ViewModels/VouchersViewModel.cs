using System.Collections.ObjectModel;
using System.Windows.Input;
using CentroMaritimo.UI.ViewModels.Base;
using CentroMaritimo.UI.ViewModels.Items;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;

namespace CentroMaritimo.UI.ViewModels;

public class VouchersViewModel : PageViewModel
{
    private readonly IVoucherService _service;
    private readonly IVoucherRepository _repo;
    private readonly IAgenciaRepository _agenciasRepo;
    private readonly IBarcoRepository _barcosRepo;
    private readonly IDialogService _dialog;

    public ObservableCollection<VoucherItem> Vouchers { get; } = [];
    public ObservableCollection<Agencia> Agencias { get; } = [];
    public ObservableCollection<Barco> Barcos { get; } = [];
    public IReadOnlyList<int> Anios { get; } = Enumerable.Range(DateTime.Today.Year - 5, 7).Reverse().ToList();
    public IReadOnlyList<int> Meses { get; } = Enumerable.Range(1, 12).ToList();

    private int _anio = DateTime.Today.Year;
    public int Anio { get => _anio; set => SetField(ref _anio, value); }

    private int _mes = DateTime.Today.Month;
    public int Mes { get => _mes; set => SetField(ref _mes, value); }

    private VoucherItem? _seleccionado;
    public VoucherItem? Seleccionado { get => _seleccionado; set => SetField(ref _seleccionado, value); }

    private Agencia? _agenciaNueva;
    public Agencia? AgenciaNueva { get => _agenciaNueva; set => SetField(ref _agenciaNueva, value); }

    private Barco? _barcoNuevo;
    public Barco? BarcoNuevo { get => _barcoNuevo; set => SetField(ref _barcoNuevo, value); }

    private DateTime _fechaNueva = DateTime.Today;
    public DateTime FechaNueva { get => _fechaNueva; set => SetField(ref _fechaNueva, value); }

    private decimal _importeNuevo;
    public decimal ImporteNuevo { get => _importeNuevo; set => SetField(ref _importeNuevo, value); }

    public ICommand BuscarCommand { get; }
    public ICommand CrearCommand { get; }
    public ICommand EliminarCommand { get; }

    public VouchersViewModel(IVoucherService service, IVoucherRepository repo, IAgenciaRepository agenciasRepo, IBarcoRepository barcosRepo, IDialogService dialog)
    {
        _service = service;
        _repo = repo;
        _agenciasRepo = agenciasRepo;
        _barcosRepo = barcosRepo;
        _dialog = dialog;
        BuscarCommand = new AsyncRelayCommand(BuscarAsync);
        CrearCommand = new AsyncRelayCommand(CrearAsync);
        EliminarCommand = new AsyncRelayCommand(EliminarAsync, () => Seleccionado is { Consolidado: false });
        _ = InicializarAsync();
    }

    private async Task InicializarAsync()
    {
        foreach (var a in await _agenciasRepo.GetActivasAsync()) Agencias.Add(a);
        foreach (var b in (await _barcosRepo.GetAllAsync()).OrderBy(b => b.Nombre)) Barcos.Add(b);
        await BuscarAsync();
    }

    private async Task BuscarAsync()
    {
        IsBusy = true;
        LimpiarStatus();
        try
        {
            var res = await _service.GetPendientesAsync(Anio, Mes);
            Vouchers.Clear();
            if (res.Success && res.Data is not null)
                foreach (var v in res.Data) Vouchers.Add(new VoucherItem(v));
        }
        finally { IsBusy = false; }
    }

    private async Task CrearAsync()
    {
        if (AgenciaNueva is null) { MostrarError("Seleccione una agencia."); return; }
        if (BarcoNuevo is null) { MostrarError("Seleccione un barco."); return; }
        if (ImporteNuevo <= 0) { MostrarError("El importe debe ser mayor a cero."); return; }

        var res = await _service.CrearVoucherAsync(AgenciaNueva.Id, BarcoNuevo.Id, FechaNueva, ImporteNuevo);
        if (!res.Success) { MostrarError(res.ErrorMessage ?? "No se pudo crear el voucher."); return; }

        MostrarExito($"Voucher Nro. {res.Data!.Numero} creado.");
        ImporteNuevo = 0;
        await BuscarAsync();
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
