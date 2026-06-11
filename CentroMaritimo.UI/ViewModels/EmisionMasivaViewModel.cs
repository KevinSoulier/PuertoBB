using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using CentroMaritimo.UI.ViewModels.Base;
using CentroMaritimo.UI.ViewModels.Items;
using PuertoBB.Core.Common;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Resultados;
using PuertoBB.Services.Common;

namespace CentroMaritimo.UI.ViewModels;

public class EmisionMasivaViewModel : PageViewModel
{
    private static readonly CultureInfo _es = new("es-AR");

    private readonly ICentroMaritimoReciboService _service;
    private readonly IGrupoFacturacionRepository _gruposRepo;
    private readonly IReciboRepository _recibosRepo;
    private readonly IDialogService _dialog;
    private readonly ICentroMaritimoPdfService _pdf;

    private CancellationTokenSource _cargarCts = new();

    public ObservableCollection<GrupoFacturacion> Grupos { get; } = [];

    /// <summary>Una fila por agencia del grupo, con el estado de su recibo de cuota en el período (o "No emitido").</summary>
    public ObservableCollection<EmisionMasivaItem> Items { get; } = [];

    public IReadOnlyList<string> MesesNombres { get; } =
        Enumerable.Range(1, 12)
            .Select(m => _es.DateTimeFormat.GetMonthName(m))
            .Select(n => char.ToUpper(n[0]) + n[1..])
            .ToList();

    private int _mesIndex = DateTime.Today.Month - 1;
    public int MesIndex
    {
        get => _mesIndex;
        set { if (SetField(ref _mesIndex, value)) { _mes = value + 1; _ = CargarEstadoAsync(); } }
    }

    private int _mes = DateTime.Today.Month;

    private int _anio = DateTime.Today.Year;
    public int Anio
    {
        get => _anio;
        set { if (SetField(ref _anio, value)) _ = CargarEstadoAsync(); }
    }

    private GrupoFacturacion? _grupo;
    public GrupoFacturacion? Grupo
    {
        get => _grupo;
        set { if (SetField(ref _grupo, value)) _ = CargarEstadoAsync(); }
    }

    private EmisionMasivaItem? _seleccionado;
    public EmisionMasivaItem? Seleccionado { get => _seleccionado; set => SetField(ref _seleccionado, value); }

    private bool _hayItems;
    public bool HayItems { get => _hayItems; set => SetField(ref _hayItems, value); }

    private string _resumen = string.Empty;
    public string Resumen { get => _resumen; set => SetField(ref _resumen, value); }

    // Acciones masivas (todo el grupo)
    public ICommand EmitirTodosCommand { get; }
    public ICommand EnviarTodosCommand { get; }
    public ICommand EmitirYEnviarTodosCommand { get; }

    // Acciones por fila (entidad seleccionada)
    public ICommand EmitirCommand { get; }
    public ICommand EnviarCommand { get; }
    public ICommand PrevisualizarCommand { get; }

    public EmisionMasivaViewModel(
        ICentroMaritimoReciboService service,
        IGrupoFacturacionRepository gruposRepo,
        IReciboRepository recibosRepo,
        IDialogService dialog,
        ICentroMaritimoPdfService pdf)
    {
        _service = service;
        _gruposRepo = gruposRepo;
        _recibosRepo = recibosRepo;
        _dialog = dialog;
        _pdf = pdf;

        EmitirTodosCommand = new AsyncRelayCommand(() => EmitirTodosAsync(enviarMail: false), () => Grupo is not null);
        EmitirYEnviarTodosCommand = new AsyncRelayCommand(() => EmitirTodosAsync(enviarMail: true), () => Grupo is not null);
        EnviarTodosCommand = new AsyncRelayCommand(EnviarTodosAsync, () => Grupo is not null);

        EmitirCommand = new AsyncRelayCommand(EmitirSeleccionadoAsync, () => Seleccionado?.EsEmitible == true);
        EnviarCommand = new AsyncRelayCommand(EnviarSeleccionadoAsync, () => Seleccionado?.EsEnviable == true);
        PrevisualizarCommand = new AsyncRelayCommand(PrevisualizarSeleccionadoAsync, () => Seleccionado?.EsPrevisualizable == true);

        _ = CargarGruposAsync();
    }

    private async Task CargarGruposAsync()
    {
        foreach (var g in await _gruposRepo.GetActivosAsync())
            Grupos.Add(g);
    }

    private async Task CargarEstadoAsync()
    {
        _cargarCts.Cancel();
        _cargarCts = new CancellationTokenSource();
        var ct = _cargarCts.Token;

        Items.Clear();
        HayItems = false;
        Resumen = string.Empty;
        if (Grupo is null) return;

        try
        {
            var res = await _service.GetEstadoMasivoAsync(Grupo.Id, _anio, _mes, ct);
            if (!res.Success || res.Data is null) { MostrarError(res.ErrorMessage ?? "No se pudo cargar el estado del grupo."); return; }

            foreach (var e in res.Data)
                Items.Add(new EmisionMasivaItem(e.EntidadId, e.EntidadNombre, e.Recibo));

            var total = Items.Count;
            var emitidos = Items.Count(i => i.CaeOk);
            Resumen = $"{total} miembro(s) · {emitidos} emitido(s) · {total - emitidos} sin emitir";
            HayItems = total > 0;
        }
        catch (OperationCanceledException) { }
    }

    private async Task EmitirTodosAsync(bool enviarMail)
    {
        if (Grupo is null) return;
        var pregunta = enviarMail
            ? $"¿Emitir y enviar los recibos del grupo «{Grupo.Nombre}» de {Formato.Periodo(_anio, _mes)} a cada agencia?"
            : $"¿Emitir (obtener CAE) los recibos pendientes del grupo «{Grupo.Nombre}» de {Formato.Periodo(_anio, _mes)}? No se enviarán por mail.";
        if (!await _dialog.ShowConfirmAsync(enviarMail ? "Emitir y enviar" : "Emitir", pregunta)) return;

        await EjecutarMasivoAsync(() => _service.EmitirMasivoAsync(Grupo.Id, _anio, _mes, enviarMail),
            enviarMail ? "Emisión y envío" : "Emisión");
    }

    private async Task EnviarTodosAsync()
    {
        if (Grupo is null) return;
        if (!await _dialog.ShowConfirmAsync("Enviar",
                $"¿Enviar por mail los recibos ya emitidos del grupo «{Grupo.Nombre}» de {Formato.Periodo(_anio, _mes)} que aún no se enviaron?")) return;

        await EjecutarMasivoAsync(() => _service.EnviarMasivoAsync(Grupo.Id, _anio, _mes), "Envío");
    }

    private async Task EjecutarMasivoAsync(Func<Task<ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>>> operacion, string accion)
    {
        LimpiarStatus();
        IsBusy = true;
        try
        {
            var res = await operacion();
            if (!res.Success) { MostrarError(res.ErrorMessage ?? "No se pudo completar la operación."); return; }

            var datos = res.Data!;
            var ok = datos.Count(r => r.Exito);
            var fallidos = datos.Where(r => !r.Exito).ToList();
            var primerError = fallidos.FirstOrDefault()?.ErrorEmision ?? "Error desconocido.";

            if (datos.Count == 0) MostrarError("No había nada para procesar en el período.");
            else if (ok == 0) MostrarError($"{accion} fallida: {fallidos.Count} omitido(s)/con error. {primerError}");
            else if (fallidos.Count > 0) MostrarAdvertencia($"{accion} parcial: {ok} ok, {fallidos.Count} omitido(s)/con error. Primer error: {primerError}");
            else MostrarExito($"{accion} finalizada: {ok} ok.");
            await CargarEstadoAsync();
        }
        finally { IsBusy = false; }
    }

    private async Task EmitirSeleccionadoAsync()
    {
        if (Grupo is null || Seleccionado is null) return;
        LimpiarStatus();
        IsBusy = true;
        try
        {
            // Sin recibo aún → crear y emitir; ya existe (Pendiente) → reintentar el CAE. En ambos casos sin mail.
            var res = Seleccionado.ReciboId is int reciboId
                ? await _service.ReintentarAsync(reciboId, enviarMail: false)
                : await _service.EmitirDeGrupoAsync(Grupo.Id, Seleccionado.EntidadId, _anio, _mes, enviarMail: false);

            if (!res.Success) { MostrarError(res.ErrorMessage ?? "No se pudo emitir."); return; }
            var r = res.Data!;
            if (r.Exito) MostrarExito($"Recibo emitido (Nro. {r.NumeroComprobante}).");
            else MostrarError(r.ErrorEmision ?? "No se pudo emitir.");
            await CargarEstadoAsync();
        }
        finally { IsBusy = false; }
    }

    private async Task EnviarSeleccionadoAsync()
    {
        if (Seleccionado?.ReciboId is not int reciboId) return;

        if (Seleccionado.MailEnviado)
        {
            var fecha = Seleccionado.FechaEnvioMailFormateada ?? "fecha desconocida";
            if (!await _dialog.ShowConfirmAsync("Reenviar mail",
                    $"Este recibo ya fue enviado el {fecha}.\n¿Desea volver a enviarlo?", "Reenviar", "Cancelar"))
                return;
        }

        LimpiarStatus();
        IsBusy = true;
        try
        {
            var res = await _service.ReenviarMailAsync(reciboId);
            if (res.Success) MostrarExito("Mail enviado correctamente.");
            else MostrarError(res.ErrorMessage ?? "No se pudo enviar el mail.");
            await CargarEstadoAsync();
        }
        finally { IsBusy = false; }
    }

    private async Task PrevisualizarSeleccionadoAsync()
    {
        if (Seleccionado?.ReciboId is not int reciboId) return;
        var recibo = await _recibosRepo.GetConDetalleAsync(reciboId);
        if (recibo is null) { MostrarError("El recibo no se encontró."); return; }
        IsBusy = true;
        try
        {
            var bytes = await _pdf.GenerarPdfReciboAsync(recibo);
            await _dialog.ShowPdfAsync(bytes, $"Recibo {Seleccionado.Comprobante}", $"Recibo_{Seleccionado.Agencia}_{Seleccionado.Comprobante}");
        }
        catch (Exception ex) { MostrarError($"No se pudo previsualizar: {ex.Message}"); }
        finally { IsBusy = false; }
    }
}
