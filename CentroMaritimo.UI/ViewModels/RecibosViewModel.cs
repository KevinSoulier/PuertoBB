using System.Collections.ObjectModel;
using System.Windows.Input;
using CentroMaritimo.UI.ViewModels.Base;
using CentroMaritimo.UI.ViewModels.Items;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;

namespace CentroMaritimo.UI.ViewModels;

public class RecibosViewModel : PageViewModel
{
    private readonly ICentroMaritimoReciboService _service;
    private readonly IReciboRepository _recibosRepo;
    private readonly IAgenciaRepository _agenciasRepo;
    private readonly IConceptoReciboRepository _conceptosRepo;
    private readonly IDialogService _dialog;
    private readonly ICentroMaritimoPdfService _pdf;

    private List<ConceptoRecibo> _todosConceptos = [];

    public ObservableCollection<ReciboItem> Recibos { get; } = [];
    public ObservableCollection<Agencia> Agencias { get; } = [];
    public ObservableCollection<ConceptoRecibo> DetallesFiltrados { get; } = [];
    public IReadOnlyList<int> Meses { get; } = Enumerable.Range(1, 12).ToList();

    private int _anio = DateTime.Today.Year;
    public int Anio { get => _anio; set => SetField(ref _anio, value); }

    private int _mes = DateTime.Today.Month;
    public int Mes { get => _mes; set => SetField(ref _mes, value); }

    private ReciboItem? _seleccionado;
    public ReciboItem? Seleccionado { get => _seleccionado; set => SetField(ref _seleccionado, value); }

    // Emisión individual
    private Agencia? _agenciaEmision;
    public Agencia? AgenciaEmision { get => _agenciaEmision; set => SetField(ref _agenciaEmision, value); }

    private int _anioEmision = DateTime.Today.Year;
    public int AnioEmision { get => _anioEmision; set => SetField(ref _anioEmision, value); }

    private int _mesEmision = DateTime.Today.Month;
    public int MesEmision { get => _mesEmision; set => SetField(ref _mesEmision, value); }

    private DateTime _fechaEmision = DateTime.Today;
    public DateTime FechaEmision { get => _fechaEmision; set => SetField(ref _fechaEmision, value); }

    private decimal _importeEmision;
    public decimal ImporteEmision { get => _importeEmision; set => SetField(ref _importeEmision, value); }

    // Autocomplete de detalle (concepto reutilizable, estilo "Barco" en Vouchers)
    private string _detalleTexto = string.Empty;
    public string DetalleTexto
    {
        get => _detalleTexto;
        set
        {
            if (SetField(ref _detalleTexto, value))
            {
                if (_detalleSeleccionado?.Nombre != value) _detalleSeleccionado = null;
                FiltrarDetalles();
            }
        }
    }

    private ConceptoRecibo? _detalleSeleccionado;
    public ConceptoRecibo? DetalleSeleccionado
    {
        get => _detalleSeleccionado;
        set
        {
            if (SetField(ref _detalleSeleccionado, value) && value is not null)
            {
                _detalleTexto = value.Nombre;
                OnPropertyChanged(nameof(DetalleTexto));
            }
        }
    }

    public ICommand BuscarCommand { get; }
    public ICommand EmitirCommand { get; }
    public ICommand EmitirYEnviarCommand { get; }
    public ICommand ReintentarCommand { get; }
    public ICommand AnularCommand { get; }
    public ICommand ReenviarCommand { get; }
    public ICommand MarcarPagadoCommand { get; }
    public ICommand PrevisualizarCommand { get; }

    public RecibosViewModel(
        ICentroMaritimoReciboService service,
        IReciboRepository recibosRepo,
        IAgenciaRepository agenciasRepo,
        IConceptoReciboRepository conceptosRepo,
        IDialogService dialog,
        ICentroMaritimoPdfService pdf)
    {
        _service = service;
        _recibosRepo = recibosRepo;
        _agenciasRepo = agenciasRepo;
        _conceptosRepo = conceptosRepo;
        _dialog = dialog;
        _pdf = pdf;

        BuscarCommand = new AsyncRelayCommand(BuscarAsync);
        EmitirCommand = new AsyncRelayCommand(() => EmitirAsync(enviarMail: false));
        EmitirYEnviarCommand = new AsyncRelayCommand(() => EmitirAsync(enviarMail: true));
        ReintentarCommand = new AsyncRelayCommand(ReintentarAsync, () => Seleccionado?.EsReintentable == true);
        AnularCommand = new AsyncRelayCommand(AnularAsync, () => Seleccionado is { } s && s.Estado != "Anulado");
        ReenviarCommand = new AsyncRelayCommand(ReenviarAsync, () => Seleccionado?.EsReenviable == true);
        MarcarPagadoCommand = new AsyncRelayCommand(MarcarPagadoAsync, () => Seleccionado?.EsPagable == true);
        PrevisualizarCommand = new AsyncRelayCommand(PrevisualizarAsync, () => Seleccionado is not null);
        _ = InicializarAsync();
    }

    private async Task InicializarAsync()
    {
        foreach (var a in await _agenciasRepo.GetActivasAsync()) Agencias.Add(a);
        await RecargarConceptosAsync();
        await BuscarAsync();
    }

    private async Task RecargarConceptosAsync()
    {
        _todosConceptos = (await _conceptosRepo.GetAllAsync()).OrderBy(c => c.Nombre).ToList();
        FiltrarDetalles();
    }

    private void FiltrarDetalles()
    {
        DetallesFiltrados.Clear();
        var texto = _detalleTexto.Trim();
        var lista = string.IsNullOrEmpty(texto)
            ? _todosConceptos
            : _todosConceptos.Where(c => c.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase));
        foreach (var c in lista) DetallesFiltrados.Add(c);
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

    private async Task EmitirAsync(bool enviarMail)
    {
        if (AgenciaEmision is null) { MostrarError("Seleccione una agencia."); return; }
        if (ImporteEmision <= 0) { MostrarError("El importe debe ser mayor a cero."); return; }
        var detalle = DetalleTexto.Trim();
        if (string.IsNullOrWhiteSpace(detalle)) { MostrarError("Ingrese un detalle."); return; }
        if (FechaEmision.Date > DateTime.Today) { MostrarError("La fecha de emisión no puede ser futura."); return; }

        var accion = enviarMail ? "Emitir y enviar" : "Emitir (sin enviar mail)";
        if (!await _dialog.ShowConfirmAsync(accion,
                $"¿Emitir recibo a {AgenciaEmision.Nombre}?\n\n" +
                $"Período: {MesEmision:00}/{AnioEmision}\n" +
                $"Fecha de emisión: {FechaEmision:dd/MM/yyyy}\n" +
                $"Importe: {ImporteEmision:C2}\n" +
                $"Detalle: {detalle}")) return;

        IsBusy = true;
        try
        {
            var res = await _service.EmitirIndividualAsync(AgenciaEmision.Id, ImporteEmision, detalle, FechaEmision, AnioEmision, MesEmision, enviarMail);
            if (!res.Success) { MostrarError(res.ErrorMessage ?? "No se pudo emitir."); return; }

            var r = res.Data!;
            if (r.Exito)
            {
                MostrarExito(MensajeExito(r.NumeroComprobante, enviarMail, r.ErrorMail));
                await GuardarConceptoAsync(detalle);
                ImporteEmision = 0;
                DetalleTexto = string.Empty;
                DetalleSeleccionado = null;
                Anio = AnioEmision; Mes = MesEmision;
                await BuscarAsync();
            }
            else
            {
                // Falló (p. ej. CAE rechazado): el recibo queda Pendiente. Mostrarlo para poder reintentar.
                Anio = AnioEmision; Mes = MesEmision;
                await BuscarAsync();
                MostrarError(r.ErrorEmision ?? "No se pudo emitir.");
            }
        }
        finally { IsBusy = false; }
    }

    private static string MensajeExito(long? numero, bool enviarMail, string? errorMail)
    {
        if (!enviarMail)
            return $"Recibo emitido (Nro. {numero}). Sin enviar mail.";
        return errorMail is null
            ? $"Recibo emitido y enviado (Nro. {numero})."
            : $"Recibo emitido (Nro. {numero}). El mail no se pudo enviar: {errorMail}";
    }

    /// <summary>Guarda el detalle como concepto reutilizable si todavía no existe.</summary>
    private async Task GuardarConceptoAsync(string detalle)
    {
        if (_todosConceptos.Any(c => c.Nombre.Equals(detalle, StringComparison.OrdinalIgnoreCase))) return;
        try
        {
            await _conceptosRepo.AddAsync(new ConceptoRecibo { Nombre = detalle });
            await RecargarConceptosAsync();
        }
        catch { /* no bloquear la emisión por el catálogo de conceptos */ }
    }

    private async Task ReintentarAsync()
    {
        if (Seleccionado is null) return;
        IsBusy = true;
        try
        {
            var res = await _service.ReintentarAsync(Seleccionado.Id, enviarMail: true);
            if (!res.Success) { MostrarError(res.ErrorMessage ?? "No se pudo reintentar."); return; }

            var r = res.Data!;
            if (r.Exito)
                MostrarExito(r.ErrorMail is null
                    ? $"Recibo completado (Nro. {r.NumeroComprobante})."
                    : $"CAE OK (Nro. {r.NumeroComprobante}), pero el mail no se pudo enviar: {r.ErrorMail}");
            else MostrarError(r.ErrorEmision ?? "No se pudo reintentar.");
            await BuscarAsync();
        }
        finally { IsBusy = false; }
    }

    private async Task AnularAsync()
    {
        if (Seleccionado is null) return;
        if (!await _dialog.ShowConfirmAsync("Anular recibo",
                $"¿Anular el recibo {Seleccionado.Comprobante} de {Seleccionado.Agencia}? Se generará una nota de crédito.",
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

        if (Seleccionado.MailEnviado)
        {
            var fecha = Seleccionado.FechaEnvioMailFormateada ?? "fecha desconocida";
            if (!await _dialog.ShowConfirmAsync("Reenviar mail",
                    $"Este recibo ya fue enviado el {fecha}.\n¿Desea volver a enviarlo?",
                    "Reenviar", "Cancelar"))
                return;
        }

        IsBusy = true;
        try
        {
            var res = await _service.ReenviarMailAsync(Seleccionado.Id);
            if (res.Success) { MostrarExito("Mail reenviado correctamente."); await BuscarAsync(); }
            else MostrarError(res.ErrorMessage ?? "No se pudo reenviar.");
        }
        finally { IsBusy = false; }
    }

    private async Task MarcarPagadoAsync()
    {
        if (Seleccionado is null) return;
        var res = await _service.MarcarPagadoAsync(Seleccionado.Id);
        if (res.Success) { MostrarExito("Recibo marcado como pagado."); await BuscarAsync(); }
        else MostrarError(res.ErrorMessage ?? "No se pudo marcar.");
    }

    private async Task PrevisualizarAsync()
    {
        if (Seleccionado is null) return;
        var recibo = await _recibosRepo.GetConDetalleAsync(Seleccionado.Id);
        if (recibo is null) { MostrarError("El recibo no se encontró."); return; }
        IsBusy = true;
        try
        {
            var bytes = await _pdf.GenerarPdfReciboAsync(recibo);
            var nombre = $"Recibo_{Seleccionado.Agencia}_{Seleccionado.Comprobante}";
            await _dialog.ShowPdfAsync(bytes, $"Recibo {Seleccionado.Comprobante}", nombre);
        }
        catch (Exception ex) { MostrarError($"No se pudo previsualizar: {ex.Message}"); }
        finally { IsBusy = false; }
    }
}
