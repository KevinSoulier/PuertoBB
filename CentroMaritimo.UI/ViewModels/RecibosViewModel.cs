using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using CentroMaritimo.UI.ViewModels.Base;
using CentroMaritimo.UI.ViewModels.Items;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models;
using PuertoBB.Services.Common;

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

    private static readonly CultureInfo _es = new("es-AR");

    public ObservableCollection<ReciboItem> Recibos { get; private set; } = [];
    public ObservableCollection<Agencia> Agencias { get; } = [];
    public IReadOnlyList<string> MesesNombres { get; } =
        Enumerable.Range(1, 12)
            .Select(m => _es.DateTimeFormat.GetMonthName(m))
            .Select(n => char.ToUpper(n[0]) + n[1..])
            .ToList();

    private int _anio = DateTime.Today.Year;
    public int Anio { get => _anio; set { if (SetField(ref _anio, value)) _ = BuscarAsync(); } }

    private int _mesIndex = DateTime.Today.Month - 1;
    public int MesIndex
    {
        get => _mesIndex;
        set { if (SetField(ref _mesIndex, value)) { _mes = value + 1; _ = BuscarAsync(); } }
    }

    private int _mes = DateTime.Today.Month;

    private List<ReciboItem> _todosRecibos = [];

    private string _textoBusqueda = string.Empty;
    public string TextoBusqueda
    {
        get => _textoBusqueda;
        set { if (SetField(ref _textoBusqueda, value)) AplicarFiltro(); }
    }

    private ReciboItem? _seleccionado;
    public ReciboItem? Seleccionado { get => _seleccionado; set => SetField(ref _seleccionado, value); }

    public ICommand BuscarCommand { get; }
    public ICommand AbrirEmisionIndividualCommand { get; }
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
        AbrirEmisionIndividualCommand = new AsyncRelayCommand(AbrirEmisionIndividualAsync);
        ReintentarCommand = new AsyncRelayCommand(ReintentarAsync, () => Seleccionado?.EsReintentable == true);
        AnularCommand = new AsyncRelayCommand(AnularAsync, () => Seleccionado?.EsAnulable == true);
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
    }

    private async Task BuscarAsync()
    {
        IsBusy = true;
        LimpiarStatus();
        try
        {
            _todosRecibos = (await _recibosRepo.GetPorPeriodoAsync(Anio, _mes))
                .Select(r => new ReciboItem(r))
                .ToList();
            AplicarFiltro();
        }
        finally { IsBusy = false; }
    }

    private void AplicarFiltro()
    {
        var texto = _textoBusqueda.Trim();
        var lista = string.IsNullOrEmpty(texto)
            ? _todosRecibos
            : _todosRecibos.Where(r =>
                r.Agencia.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                r.Comprobante.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                r.Periodo.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                r.Importe.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                r.Estado.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                r.EstadoEnvio.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                r.FechaEmision.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                (r.Error ?? "").Contains(texto, StringComparison.OrdinalIgnoreCase));
        Recibos = new ObservableCollection<ReciboItem>(lista);
        OnPropertyChanged(nameof(Recibos));
    }

    private async Task AbrirEmisionIndividualAsync()
    {
        var entidades = Agencias.Select(a => new EntidadEmisionItem(a.Id, a.Nombre)).ToList();
        var conceptos = _todosConceptos.Select(c => c.Nombre).ToList();

        var result = await _dialog.ShowEmisionIndividualAsync("Agencia", entidades, conceptos);
        if (result is null) return;

        var agencia = Agencias.FirstOrDefault(a => a.Id == result.EntidadId);
        if (agencia is null) { MostrarError("No se encontró la agencia seleccionada."); return; }

        var mes  = result.FechaEmision.Month;
        var anio = result.FechaEmision.Year;
        var detalleResumen = $"{result.Lineas.Count} ítem(s)";

        IsBusy = true;
        try
        {
            var res = await _service.EmitirIndividualAsync(agencia.Id, result.Lineas.Sum(l => l.Importe), detalleResumen, result.FechaEmision, anio, mes, result.EnviarMail, result.Lineas);
            if (!res.Success) { MostrarError(res.ErrorMessage ?? "No se pudo emitir."); return; }

            var r = res.Data!;
            if (r.Exito)
            {
                MostrarExito(MensajeExito(r.NumeroComprobante, result.EnviarMail, r.ErrorMail));
                Anio = anio; MesIndex = mes - 1;
                await BuscarAsync();
                await GuardarConceptosAsync(result.Lineas);
            }
            else
            {
                Anio = anio; MesIndex = mes - 1;
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

    private async Task GuardarConceptosAsync(IEnumerable<ReciboLineaInput> lineas)
    {
        foreach (var linea in lineas)
            await GuardarConceptoAsync(linea.Descripcion);
    }

    private async Task GuardarConceptoAsync(string detalle)
    {
        if (_todosConceptos.Any(c => c.Nombre.Equals(detalle, StringComparison.OrdinalIgnoreCase))) return;
        try
        {
            await _conceptosRepo.AddAsync(new ConceptoRecibo { Nombre = detalle });
            await RecargarConceptosAsync();
        }
        catch { /* no bloquear por el catálogo de conceptos */ }
    }

    private async Task ReintentarAsync()
    {
        if (Seleccionado is null) return;
        IsBusy = true;
        try
        {
            var res = await _service.ReintentarAsync(Seleccionado.Id, enviarMail: false);
            if (!res.Success) { MostrarError(res.ErrorMessage ?? "No se pudo reintentar."); return; }

            var r = res.Data!;
            if (r.Exito)
                MostrarExito($"CAE obtenido. Recibo completado (Nro. {r.NumeroComprobante}).");
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
            var bytes = recibo.EsConsolidadoVouchers
                ? await _pdf.GenerarPdfDescargaAsync(recibo.Vouchers.OrderBy(v => v.Numero).ToList(), recibo)
                : await _pdf.GenerarPdfReciboAsync(recibo);
            var nombre = $"Recibo_{Seleccionado.Agencia}_{Seleccionado.Comprobante}";
            await _dialog.ShowPdfAsync(bytes, $"Recibo {Seleccionado.Comprobante}", nombre);
        }
        catch (Exception ex) { MostrarError($"No se pudo previsualizar: {ex.Message}"); }
        finally { IsBusy = false; }
    }
}
