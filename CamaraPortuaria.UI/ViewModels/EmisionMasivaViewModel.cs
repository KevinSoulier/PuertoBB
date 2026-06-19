using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using CamaraPortuaria.UI.ViewModels.Base;
using CamaraPortuaria.UI.ViewModels.Items;
using PuertoBB.Core.Common;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Resultados;
using PuertoBB.Services.Common;

namespace CamaraPortuaria.UI.ViewModels;

public class EmisionMasivaViewModel : PageViewModel
{
    private static readonly CultureInfo _es = new("es-AR");

    private readonly ICamaraPortuariaReciboService _service;
    private readonly IGrupoFacturacionRepository _gruposRepo;
    private readonly IReciboRepository _recibosRepo;
    private readonly IDialogService _dialog;
    private readonly ICamaraPortuariaPdfService _pdf;

    private CancellationTokenSource _cargarCts = new();

    public ObservableCollection<GrupoFacturacion> Grupos { get; } = [];

    /// <summary>Una fila por empresa del grupo, con el estado de su recibo en el período (o "No emitido").</summary>
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
        set { if (SetField(ref _mesIndex, value)) { _mes = value + 1; CargarSeguro(CargarEstadoAsync); } }
    }

    private int _mes = DateTime.Today.Month;

    private int _anio = DateTime.Today.Year;
    public int Anio
    {
        get => _anio;
        set { if (SetField(ref _anio, value)) CargarSeguro(CargarEstadoAsync); }
    }

    private GrupoFacturacion? _grupo;
    public GrupoFacturacion? Grupo
    {
        get => _grupo;
        set { if (SetField(ref _grupo, value)) CargarSeguro(CargarEstadoAsync); }
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
    public ICommand EliminarCommand { get; }

    public EmisionMasivaViewModel(
        ICamaraPortuariaReciboService service,
        IGrupoFacturacionRepository gruposRepo,
        IReciboRepository recibosRepo,
        IDialogService dialog,
        ICamaraPortuariaPdfService pdf)
    {
        _service = service;
        _gruposRepo = gruposRepo;
        _recibosRepo = recibosRepo;
        _dialog = dialog;
        _pdf = pdf;

        EmitirTodosCommand = new AsyncRelayCommand(() => EmitirTodosAsync(enviarMail: false), () => Grupo is not null);
        EmitirYEnviarTodosCommand = new AsyncRelayCommand(() => EmitirTodosAsync(enviarMail: true), () => Grupo is not null);
        EnviarTodosCommand = new AsyncRelayCommand(EnviarTodosAsync, () => Grupo is not null && Items.Any(i => i.EsEnviable));

        EmitirCommand = new AsyncRelayCommand(EmitirSeleccionadoAsync, () => Seleccionado?.EsEmitible == true);
        EnviarCommand = new AsyncRelayCommand(EnviarSeleccionadoAsync, () => Seleccionado?.EsEnviable == true);
        PrevisualizarCommand = new AsyncRelayCommand(PrevisualizarSeleccionadoAsync, () => Seleccionado?.EsPrevisualizable == true);
        EliminarCommand = new AsyncRelayCommand(EliminarSeleccionadoAsync, () => Seleccionado?.EsEliminable == true);

        CargarSeguro(CargarGruposAsync);
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
                Items.Add(new EmisionMasivaItem(e.EntidadId, e.EntidadNombre, e.Recibo, e.ImporteEsperado));

            var total = Items.Count;
            var emitidos = Items.Count(i => i.CaeOk);
            Resumen = $"{total} miembro(s) · {emitidos} emitido(s) · {total - emitidos} sin emitir";
            HayItems = total > 0;
            CommandManager.InvalidateRequerySuggested();
        }
        catch (OperationCanceledException) { }
    }

    private async Task EmitirTodosAsync(bool enviarMail)
    {
        if (Grupo is not { } grupo) return;
        var pregunta = enviarMail
            ? $"¿Emitir y enviar los recibos del grupo «{grupo.Nombre}» de {Formato.Periodo(_anio, _mes)} a cada empresa?"
            : $"¿Emitir (obtener CAE) los recibos pendientes del grupo «{grupo.Nombre}» de {Formato.Periodo(_anio, _mes)}? No se enviarán por mail.";
        if (!await _dialog.ShowConfirmAsync(enviarMail ? "Emitir y enviar" : "Emitir", pregunta)) return;

        // Idempotente: lo ya emitido/enviado se omite (no falla). Si hay ya enviados, preguntar si reenviarlos.
        var reenviar = false;
        if (enviarMail)
        {
            var yaEnviados = Items.Count(i => i.MailEnviado);
            if (yaEnviados > 0)
                reenviar = await _dialog.ShowConfirmAsync("Reenviar comprobantes",
                    $"{yaEnviados} recibo(s) del grupo ya fueron enviados por mail. ¿Reenviarlos también?",
                    "Reenviar todos", "Solo lo pendiente");
        }

        await EjecutarMasivoAsync(
            enviarMail ? "Emitiendo y enviando" : "Emitiendo",
            (progreso, ct) => _service.EmitirMasivoAsync(grupo.Id, _anio, _mes, enviarMail, reenviar, progreso, ct),
            enviarMail ? "Emisión y envío" : "Emisión");
    }

    private async Task EnviarTodosAsync()
    {
        if (Grupo is not { } grupo) return;
        if (!await _dialog.ShowConfirmAsync("Enviar",
                $"¿Enviar por mail los recibos ya emitidos del grupo «{grupo.Nombre}» de {Formato.Periodo(_anio, _mes)} que aún no se enviaron?")) return;

        await EjecutarMasivoAsync(
            "Enviando",
            (progreso, ct) => _service.EnviarMasivoAsync(grupo.Id, _anio, _mes, progreso, ct),
            "Envío");
    }

    private async Task EjecutarMasivoAsync(
        string titulo,
        Func<IProgress<ProgresoMasivo>, CancellationToken, Task<ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>>> operacion,
        string accion)
    {
        LimpiarStatus();
        var res = await EjecutarConProgresoAsync(titulo, operacion);
        if (res is null) { await CargarEstadoAsync(); return; }   // cancelado por el usuario
        if (!res.Success) { MostrarError(res.ErrorMessage ?? "No se pudo completar la operación."); return; }

        var datos = res.Data!;
        var ok = datos.Count(r => r.Exito);
        var omitidos = datos.Count(r => r.Omitido);                       // ya completos: NO son error
        var fallidos = datos.Where(r => !r.Exito && !r.Omitido).ToList();
        var primerError = fallidos.FirstOrDefault()?.ErrorEmision ?? "Error desconocido.";

        if (datos.Count == 0)
            MostrarError("No había nada para procesar en el período.");
        else if (fallidos.Count == 0)                                     // sin errores reales: éxito (aunque haya omitidos)
            MostrarExito(omitidos > 0
                ? $"{accion} finalizada: {ok} procesado(s), {omitidos} ya estaba(n) al día."
                : $"{accion} finalizada: {ok} ok.");
        else if (ok == 0 && omitidos == 0)
            MostrarError($"{accion} fallida: {fallidos.Count} con error. {primerError}");
        else
            MostrarAdvertencia($"{accion} parcial: {ok} ok, {omitidos} ya estaba(n), {fallidos.Count} con error. Primer error: {primerError}");
        await CargarEstadoAsync();
    }

    private async Task EmitirSeleccionadoAsync()
    {
        if (Grupo is not { } grupo || Seleccionado is not { } sel) return;
        LimpiarStatus();
        await EjecutarOcupadoAsync("Emitiendo recibo", async () =>
        {
            // Siempre vía el grupo: re-sincroniza importe/líneas del grupo ACTUAL antes de (re)intentar el CAE,
            // así un Pendiente trabado por datos viejos (p. ej. monto cero ya corregido en el grupo) se recupera.
            // (EmitirDeGrupoAsync sirve para "sin recibo" y para "Pendiente existente".)
            var res = await _service.EmitirDeGrupoAsync(grupo.Id, sel.EntidadId, _anio, _mes, enviarMail: false);

            if (!res.Success) { MostrarError(res.ErrorMessage ?? "No se pudo emitir."); return; }
            var r = res.Data!;
            if (r.Exito) MostrarExito($"Recibo emitido (Nro. {r.NumeroComprobante}).");
            else MostrarError(r.ErrorEmision ?? "No se pudo emitir.");
            await CargarEstadoAsync();
        });
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
        await EjecutarOcupadoAsync("Enviando", async () =>
        {
            var res = await _service.ReenviarMailAsync(reciboId);
            if (res.Success) MostrarExito("Mail enviado correctamente.");
            else MostrarError(res.ErrorMessage ?? "No se pudo enviar el mail.");
            await CargarEstadoAsync();
        });
    }

    private async Task EliminarSeleccionadoAsync()
    {
        if (Seleccionado is not { ReciboId: int reciboId } sel) return;
        if (!await _dialog.ShowConfirmAsync("Eliminar recibo",
                $"¿Eliminar el recibo Pendiente de {sel.Empresa}? No tiene CAE y esta acción no se puede deshacer.",
                "Eliminar", "Cancelar")) return;

        LimpiarStatus();
        await EjecutarOcupadoAsync("Eliminando recibo", async () =>
        {
            var res = await _service.EliminarReciboPendienteAsync(reciboId);
            if (res.Success) MostrarExito("Recibo eliminado.");
            else MostrarError(res.ErrorMessage ?? "No se pudo eliminar.");
            await CargarEstadoAsync();
        });
    }

    private async Task PrevisualizarSeleccionadoAsync()
    {
        if (Seleccionado is not { ReciboId: int reciboId } sel) return;
        await EjecutarOcupadoAsync("Generando PDF", async () =>
        {
            var recibo = await _recibosRepo.GetConDetalleAsync(reciboId);
            if (recibo is null) { MostrarError("El recibo no se encontró."); return; }
            try
            {
                var bytes = await _pdf.GenerarPdfReciboAsync(recibo);
                await _dialog.ShowPdfAsync(bytes, $"Recibo {sel.Comprobante}", $"Recibo_{sel.Empresa}_{sel.Comprobante}");
            }
            catch (Exception ex) { MostrarError($"No se pudo previsualizar: {ex.Message}"); }
        });
    }
}
