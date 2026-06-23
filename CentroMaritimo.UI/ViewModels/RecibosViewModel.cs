using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using CentroMaritimo.UI.ViewModels.Base;
using CentroMaritimo.UI.ViewModels.Items;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Enums;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models;
using PuertoBB.Core.Models.Resultados;
using PuertoBB.Services.Common;

namespace CentroMaritimo.UI.ViewModels;

public class RecibosViewModel : PageViewModel
{
    private readonly ICentroMaritimoReciboService _service;
    private readonly IReciboRepository _recibosRepo;
    private readonly IClienteRepository _agenciasRepo;
    private readonly IConceptoReciboRepository _conceptosRepo;
    private readonly IDialogService _dialog;
    private readonly ICentroMaritimoPdfService _pdf;

    private List<ConceptoRecibo> _todosConceptos = [];

    private static readonly CultureInfo _es = new("es-AR");

    public ObservableCollection<ReciboItem> Recibos { get; private set; } = [];
    public ObservableCollection<Cliente> Clientes { get; } = [];
    public IReadOnlyList<string> MesesNombres { get; } =
        Enumerable.Range(1, 12)
            .Select(m => _es.DateTimeFormat.GetMonthName(m))
            .Select(n => char.ToUpper(n[0]) + n[1..])
            .ToList();

    private int _anio = DateTime.Today.Year;
    public int Anio { get => _anio; set { if (SetField(ref _anio, value)) CargarSeguro(BuscarAsync); } }

    private int _mesIndex = DateTime.Today.Month - 1;
    public int MesIndex
    {
        get => _mesIndex;
        set { if (SetField(ref _mesIndex, value)) { _mes = value + 1; CargarSeguro(BuscarAsync); } }
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
    public ReciboItem? Seleccionado
    {
        get => _seleccionado;
        // La re-selección programática (post-recarga) no genera input: forzar el requery de la toolbar.
        set { if (SetField(ref _seleccionado, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public ICommand BuscarCommand { get; }
    public ICommand AbrirEmisionIndividualCommand { get; }
    public ICommand ReintentarCommand { get; }
    public ICommand AnularCommand { get; }
    public ICommand AnularYEnviarCommand { get; }
    public ICommand ReenviarCommand { get; }
    public ICommand MarcarPagadoCommand { get; }
    public ICommand PrevisualizarCommand { get; }
    public ICommand PrevisualizarNotaCreditoCommand { get; }
    public ICommand EditarCommand { get; }
    public ICommand EliminarCommand { get; }

    public RecibosViewModel(
        ICentroMaritimoReciboService service,
        IReciboRepository recibosRepo,
        IClienteRepository agenciasRepo,
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
        AnularYEnviarCommand = new AsyncRelayCommand(AnularYEnviarAsync, () => Seleccionado?.EsAnulable == true);
        ReenviarCommand = new AsyncRelayCommand(ReenviarAsync, () => Seleccionado?.EsReenviable == true);
        MarcarPagadoCommand = new AsyncRelayCommand(MarcarPagadoAsync, () => Seleccionado?.EsPagable == true);
        PrevisualizarCommand = new AsyncRelayCommand(PrevisualizarAsync, () => Seleccionado is not null);
        PrevisualizarNotaCreditoCommand = new AsyncRelayCommand(PrevisualizarNotaCreditoAsync, () => Seleccionado?.TieneNotaCredito == true);
        EditarCommand = new AsyncRelayCommand(EditarAsync, () => Seleccionado?.EsEditable == true);
        EliminarCommand = new AsyncRelayCommand(EliminarAsync, () => Seleccionado?.EsEditable == true);
        CargarSeguro(InicializarAsync);
    }

    private async Task InicializarAsync()
    {
        foreach (var a in await _agenciasRepo.GetActivasAsync()) Clientes.Add(a);
        await RecargarConceptosAsync();
        await BuscarAsync();
    }

    private async Task RecargarConceptosAsync()
    {
        _todosConceptos = (await _conceptosRepo.GetAllAsync()).OrderBy(c => c.Nombre).ToList();
    }

    private Task BuscarAsync()
    {
        LimpiarStatus();
        return EjecutarOcupadoAsync("Cargando recibos", async () =>
        {
            _todosRecibos = (await _recibosRepo.GetPorPeriodoAsync(Anio, _mes))
                .Select(r => new ReciboItem(r))
                .ToList();
            AplicarFiltro();
        });
    }

    private void AplicarFiltro()
    {
        var texto = _textoBusqueda.Trim();
        var lista = string.IsNullOrEmpty(texto)
            ? _todosRecibos
            : _todosRecibos.Where(r =>
                r.Cliente.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                r.Comprobante.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                r.Periodo.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                r.Importe.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                r.Estado.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                r.EstadoEnvio.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                r.FechaEmision.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                r.Cae.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                (r.Error ?? "").Contains(texto, StringComparison.OrdinalIgnoreCase));
        // Reconstruir la colección borra la selección del DataGrid: re-seleccionar por Id
        // para que la toolbar (que opera sobre Seleccionado) sobreviva a recargas y filtrado.
        var seleccionadoId = Seleccionado?.Id;
        Recibos = new ObservableCollection<ReciboItem>(lista);
        OnPropertyChanged(nameof(Recibos));
        if (seleccionadoId is int id)
            Seleccionado = Recibos.FirstOrDefault(r => r.Id == id);
    }

    private async Task AbrirEmisionIndividualAsync()
    {
        var entidades = Clientes.Select(a => new ClienteEmisionItem(a.Id, a.Nombre)).ToList();
        var conceptos = _todosConceptos.Select(c => c.Nombre).ToList();

        if (await _dialog.ShowEmisionIndividualAsync("Agencia", entidades, conceptos) is not { } result) return;

        if (Clientes.FirstOrDefault(a => a.Id == result.ClienteId) is not { } agencia)
        {
            MostrarError("No se encontró la agencia seleccionada."); return;
        }

        var mes  = result.FechaEmision.Month;
        var anio = result.FechaEmision.Year;
        var detalleResumen = $"{result.Lineas.Count} ítem(s)";

        await EjecutarOcupadoAsync(result.EnviarMail ? "Emitiendo y enviando" : "Emitiendo recibo", async () =>
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
        });
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
        if (Seleccionado is not { } sel) return;
        await EjecutarOcupadoAsync("Emitiendo recibo", async () =>
        {
            var res = await _service.ReintentarAsync(sel.Id, enviarMail: false);
            if (!res.Success) { MostrarError(res.ErrorMessage ?? "No se pudo reintentar."); return; }

            var r = res.Data!;
            if (r.Exito)
                MostrarExito($"CAE obtenido. Recibo completado (Nro. {r.NumeroComprobante}).");
            else MostrarError(r.ErrorEmision ?? "No se pudo reintentar.");
            await BuscarAsync();
        });
    }

    private async Task EditarAsync()
    {
        if (Seleccionado is not { } sel) return;
        var recibo = await _recibosRepo.GetConDetalleAsync(sel.Id);
        if (recibo is null) { MostrarError("El recibo no se encontró."); return; }

        var lineasActuales = recibo.Lineas.OrderBy(l => l.Orden)
            .Select(l => new ReciboLineaInput(l.Descripcion, l.Cantidad, l.PrecioUnitario)).ToList();
        var conceptos = _todosConceptos.Select(c => c.Nombre).ToList();
        if (await _dialog.ShowEditarReciboAsync(lineasActuales, conceptos) is not { } nuevas) return;

        await EjecutarOcupadoAsync("Guardando recibo", async () =>
        {
            var res = await _service.EditarReciboPendienteAsync(sel.Id, nuevas);
            if (res.Success) { MostrarExito("Recibo actualizado. Emitilo cuando quieras."); await GuardarConceptosAsync(nuevas); await BuscarAsync(); }
            else MostrarError(res.ErrorMessage ?? "No se pudo editar.");
        });
    }

    private async Task EliminarAsync()
    {
        if (Seleccionado is not { } sel) return;
        if (!await _dialog.ShowConfirmAsync("Eliminar recibo",
                $"¿Eliminar el recibo Pendiente de {sel.Cliente} ({sel.Periodo})? No tiene CAE y esta acción no se puede deshacer.",
                "Eliminar", "Cancelar")) return;

        await EjecutarOcupadoAsync("Eliminando recibo", async () =>
        {
            var res = await _service.EliminarReciboPendienteAsync(sel.Id);
            if (res.Success) { MostrarExito("Recibo eliminado."); await BuscarAsync(); }
            else MostrarError(res.ErrorMessage ?? "No se pudo eliminar.");
        });
    }

    private Task AnularAsync() => AnularInternoAsync(enviarMail: false);
    private Task AnularYEnviarAsync() => AnularInternoAsync(enviarMail: true);

    private async Task AnularInternoAsync(bool enviarMail)
    {
        if (Seleccionado is not { } sel) return;
        var detalleMail = enviarMail
            ? "Se generará una nota de crédito y se enviará por mail."
            : "Se generará una nota de crédito. No se enviará por mail.";
        if (!await _dialog.ShowConfirmAsync("Anular recibo",
                $"¿Anular el recibo {sel.Comprobante} de {sel.Cliente}? {detalleMail}",
                "Anular", "Cancelar")) return;
        await EjecutarOcupadoAsync(enviarMail ? "Anulando y enviando" : "Anulando", async () =>
        {
            var res = await _service.AnularReciboAsync(sel.Id, enviarMail);
            if (res.Success && res.Data is { } nc)
            {
                MostrarExito(MensajeAnulacion(nc, enviarMail));
                await BuscarAsync();
            }
            else MostrarError(res.ErrorMessage ?? "No se pudo anular.");
        });
    }

    private static string MensajeAnulacion(ResultadoAnulacion nc, bool enviarMail)
    {
        var comp = Formato.Comprobante(nc.PuntoDeVenta, nc.NumeroComprobante);
        if (!enviarMail)
            return $"Recibo anulado. Nota de crédito {comp} emitida. Sin enviar mail.";
        return nc.ErrorMail is null
            ? $"Recibo anulado. Nota de crédito {comp} emitida y enviada por mail."
            : $"Recibo anulado. Nota de crédito {comp} emitida. El mail no se pudo enviar: {nc.ErrorMail}";
    }

    private async Task ReenviarAsync()
    {
        if (Seleccionado is not { } sel) return;
        var esNotaCredito = sel.EstadoFiscal == EstadoFiscal.Anulado;

        if (esNotaCredito)
        {
            // No se persiste el envío de la NC: confirmar siempre.
            if (!await _dialog.ShowConfirmAsync("Enviar nota de crédito",
                    $"Se enviará por mail la nota de crédito {sel.NotaCreditoComprobante} (anula el recibo {sel.Comprobante}).\n¿Continuar?",
                    "Enviar", "Cancelar"))
                return;
        }
        else if (sel.MailEnviado)
        {
            var fecha = sel.FechaEnvioMailFormateada ?? "fecha desconocida";
            if (!await _dialog.ShowConfirmAsync("Reenviar mail",
                    $"Este recibo ya fue enviado el {fecha}.\n¿Desea volver a enviarlo?",
                    "Reenviar", "Cancelar"))
                return;
        }

        await EjecutarOcupadoAsync("Enviando", async () =>
        {
            var res = await _service.ReenviarMailAsync(sel.Id);
            if (res.Success)
            {
                MostrarExito(esNotaCredito ? "Nota de crédito enviada por mail." : "Mail reenviado correctamente.");
                await BuscarAsync();
            }
            else MostrarError(res.ErrorMessage ?? "No se pudo reenviar.");
        });
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
        if (Seleccionado is not { } sel) return;
        byte[]? bytes = null;
        await EjecutarOcupadoAsync("Generando PDF", async () =>
        {
            var recibo = await _recibosRepo.GetConDetalleAsync(sel.Id);
            if (recibo is null) { MostrarError("El recibo no se encontró."); return; }
            try
            {
                bytes = recibo.EsConsolidadoVouchers
                    ? await _pdf.GenerarPdfDescargaAsync(recibo.Vouchers.OrderBy(v => v.Numero).ToList(), recibo)
                    : await _pdf.GenerarPdfReciboAsync(recibo);
            }
            catch (Exception ex) { MostrarError($"No se pudo previsualizar: {ex.Message}"); }
        });
        if (bytes is null) return;
        await _dialog.ShowPdfAsync(bytes, $"Recibo {sel.Comprobante}", $"Recibo_{sel.Cliente}_{sel.Comprobante}");
    }

    private async Task PrevisualizarNotaCreditoAsync()
    {
        if (Seleccionado is not { } sel) return;
        byte[]? bytes = null;
        var comp = string.Empty;
        await EjecutarOcupadoAsync("Generando PDF", async () =>
        {
            // El PDF de NC necesita ReciboOriginal + Lineas: siempre desde el detalle, nunca desde el item de grilla.
            var recibo = await _recibosRepo.GetConDetalleAsync(sel.Id);
            if (recibo?.NotaDeCredito is null) { MostrarError("El recibo no tiene nota de crédito."); return; }
            var nota = recibo.NotaDeCredito;
            nota.ReciboOriginal ??= recibo;
            try
            {
                bytes = await _pdf.GenerarPdfNotaDeCreditoAsync(nota);
                comp = Formato.Comprobante(nota.PuntoDeVenta, nota.NumeroComprobante);
            }
            catch (Exception ex) { MostrarError($"No se pudo previsualizar: {ex.Message}"); }
        });
        if (bytes is null) return;
        await _dialog.ShowPdfAsync(bytes, $"Nota de crédito {comp}", $"NotaCredito_{sel.Cliente}_{comp}");
    }
}
