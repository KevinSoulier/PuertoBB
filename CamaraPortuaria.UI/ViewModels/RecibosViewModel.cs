using System.Collections.ObjectModel;
using System.Windows.Input;
using CamaraPortuaria.UI.ViewModels.Base;
using CamaraPortuaria.UI.ViewModels.Items;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Services;

namespace CamaraPortuaria.UI.ViewModels;

public class RecibosViewModel : PageViewModel
{
    private readonly ICamaraPortuariaReciboService _service;
    private readonly IReciboRepository _recibosRepo;
    private readonly IEmpresaRepository _empresasRepo;
    private readonly IDialogService _dialog;

    public ObservableCollection<ReciboItem> Recibos { get; } = [];
    public ObservableCollection<Empresa> Empresas { get; } = [];
    public IReadOnlyList<int> Anios { get; } = Enumerable.Range(DateTime.Today.Year - 5, 7).Reverse().ToList();
    public IReadOnlyList<int> Meses { get; } = Enumerable.Range(1, 12).ToList();

    private int _anio = DateTime.Today.Year;
    public int Anio { get => _anio; set => SetField(ref _anio, value); }

    private int _mes = DateTime.Today.Month;
    public int Mes { get => _mes; set => SetField(ref _mes, value); }

    private ReciboItem? _seleccionado;
    public ReciboItem? Seleccionado { get => _seleccionado; set => SetField(ref _seleccionado, value); }

    // Emisión individual
    private Empresa? _empresaEmision;
    public Empresa? EmpresaEmision { get => _empresaEmision; set => SetField(ref _empresaEmision, value); }

    private decimal _importeEmision;
    public decimal ImporteEmision { get => _importeEmision; set => SetField(ref _importeEmision, value); }

    private string _detalleEmision = string.Empty;
    public string DetalleEmision { get => _detalleEmision; set => SetField(ref _detalleEmision, value); }

    public ICommand BuscarCommand { get; }
    public ICommand EmitirIndividualCommand { get; }
    public ICommand AnularCommand { get; }
    public ICommand ReenviarCommand { get; }
    public ICommand MarcarPagadoCommand { get; }

    public RecibosViewModel(
        ICamaraPortuariaReciboService service,
        IReciboRepository recibosRepo,
        IEmpresaRepository empresasRepo,
        IDialogService dialog)
    {
        _service = service;
        _recibosRepo = recibosRepo;
        _empresasRepo = empresasRepo;
        _dialog = dialog;

        BuscarCommand = new AsyncRelayCommand(BuscarAsync);
        EmitirIndividualCommand = new AsyncRelayCommand(EmitirIndividualAsync);
        AnularCommand = new AsyncRelayCommand(AnularAsync, () => Seleccionado is { } s && s.Estado != "Anulado");
        ReenviarCommand = new AsyncRelayCommand(ReenviarAsync, () => Seleccionado?.EsReenviable == true);
        MarcarPagadoCommand = new AsyncRelayCommand(MarcarPagadoAsync, () => Seleccionado?.EsPagable == true);

        _ = InicializarAsync();
    }

    private async Task InicializarAsync()
    {
        foreach (var e in await _empresasRepo.GetActivasAsync())
            Empresas.Add(e);
        await BuscarAsync();
    }

    private async Task BuscarAsync()
    {
        IsBusy = true;
        LimpiarStatus();
        try
        {
            var lista = await _recibosRepo.GetPorPeriodoAsync(Anio, Mes);
            Recibos.Clear();
            foreach (var r in lista) Recibos.Add(new ReciboItem(r));
        }
        finally { IsBusy = false; }
    }

    private async Task EmitirIndividualAsync()
    {
        if (EmpresaEmision is null) { MostrarError("Seleccione una empresa."); return; }
        if (ImporteEmision <= 0) { MostrarError("El importe debe ser mayor a cero."); return; }
        if (string.IsNullOrWhiteSpace(DetalleEmision)) { MostrarError("Ingrese un detalle."); return; }

        if (!await _dialog.ShowConfirmAsync("Emitir recibo",
                $"¿Emitir recibo a {EmpresaEmision.Nombre} por {ImporteEmision:C2}?")) return;

        IsBusy = true;
        try
        {
            var res = await _service.EmitirIndividualAsync(EmpresaEmision.Id, ImporteEmision, DetalleEmision, Anio, Mes);
            if (!res.Success) { MostrarError(res.ErrorMessage ?? "No se pudo emitir."); return; }

            var r = res.Data!;
            if (r.Exito)
            {
                MostrarExito(r.ErrorMail is null
                    ? $"Recibo emitido y enviado (Nro. {r.NumeroComprobante})."
                    : $"Recibo emitido (Nro. {r.NumeroComprobante}). El mail no se pudo enviar: {r.ErrorMail}");
                ImporteEmision = 0; DetalleEmision = string.Empty;
                await BuscarAsync();
            }
            else MostrarError(r.ErrorEmision ?? "No se pudo emitir.");
        }
        finally { IsBusy = false; }
    }

    private async Task AnularAsync()
    {
        if (Seleccionado is null) return;
        if (!await _dialog.ShowConfirmAsync("Anular recibo",
                $"¿Anular el recibo {Seleccionado.Comprobante} de {Seleccionado.Empresa}? Se generará una nota de crédito.",
                "Anular", "Cancelar")) return;

        IsBusy = true;
        try
        {
            var res = await _service.AnularReciboAsync(Seleccionado.Id, enviarMail: true);
            if (res.Success) { MostrarExito("Recibo anulado y nota de crédito generada."); await BuscarAsync(); }
            else MostrarError(res.ErrorMessage ?? "No se pudo anular.");
        }
        finally { IsBusy = false; }
    }

    private async Task ReenviarAsync()
    {
        if (Seleccionado is null) return;
        var res = await _service.ReenviarMailAsync(Seleccionado.Id);
        if (res.Success) { MostrarExito("Recibo reenviado."); await BuscarAsync(); }
        else MostrarError(res.ErrorMessage ?? "No se pudo reenviar.");
    }

    private async Task MarcarPagadoAsync()
    {
        if (Seleccionado is null) return;
        var res = await _service.MarcarPagadoAsync(Seleccionado.Id);
        if (res.Success) { MostrarExito("Recibo marcado como pagado."); await BuscarAsync(); }
        else MostrarError(res.ErrorMessage ?? "No se pudo marcar.");
    }
}
