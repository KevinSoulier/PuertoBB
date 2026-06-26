using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using CentroMaritimo.UI.ViewModels.Base;
using Microsoft.Win32;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Resultados;
using PuertoBB.Services.Common;

namespace CentroMaritimo.UI.ViewModels;

public class CierrePeriodoViewModel : PageViewModel
{
    private readonly IVoucherService _voucherService;
    private readonly ICentroMaritimoReciboService _reciboService;
    private readonly IVoucherRepository _voucherRepo;
    private readonly IReciboRepository _reciboRepo;
    private readonly ICentroMaritimoPdfService _pdf;
    private readonly IDialogService _dialog;

    private static readonly CultureInfo _es = new("es-AR");

    public ObservableCollection<ClienteCierrePeriodoVm> Clientes { get; } = [];

    public IReadOnlyList<string> MesesNombres { get; } =
        Enumerable.Range(1, 12)
            .Select(m => _es.DateTimeFormat.GetMonthName(m))
            .Select(n => char.ToUpper(n[0]) + n[1..])
            .ToList();

    private int _anio = DateTime.Today.Year;
    public int Anio { get => _anio; set { if (SetField(ref _anio, value)) CargarSeguro(CargarAsync); } }

    private int _mesIndex = DateTime.Today.Month - 1;
    public int MesIndex
    {
        get => _mesIndex;
        set { if (SetField(ref _mesIndex, value)) { _mes = value + 1; CargarSeguro(CargarAsync); } }
    }

    private int _mes = DateTime.Today.Month;

    private string _resumen = string.Empty;
    public string Resumen { get => _resumen; set => SetField(ref _resumen, value); }

    private decimal _totalPeriodo;
    public decimal TotalPeriodo { get => _totalPeriodo; set { SetField(ref _totalPeriodo, value); OnPropertyChanged(nameof(TotalPeriodoTexto)); } }
    public string TotalPeriodoTexto => Formato.Moneda(_totalPeriodo);

    public ICommand CargarCommand { get; }
    public ICommand EmitirReciboClienteCommand { get; }
    public ICommand EmitirYEnviarReciboClienteCommand { get; }
    public ICommand PrevisualizarLibresCommand { get; }
    public ICommand PrevisualizarReciboCommand { get; }
    public ICommand DescargarReciboCommand { get; }
    public ICommand EnviarMailReciboCommand { get; }
    public ICommand EmitirRecibosCommand { get; }
    public ICommand EnviarMailsCommand { get; }
    public ICommand CerrarCommand { get; }
    // Acciones por voucher (facturación individual): Ver PDF / Emitir / Emitir y Enviar.
    public ICommand VerPdfVoucherCommand { get; }
    public ICommand EmitirVoucherCommand { get; }
    public ICommand EmitirYEnviarVoucherCommand { get; }

    public CierrePeriodoViewModel(
        IVoucherService voucherService,
        ICentroMaritimoReciboService reciboService,
        IVoucherRepository voucherRepo,
        IReciboRepository reciboRepo,
        ICentroMaritimoPdfService pdf,
        IDialogService dialog)
    {
        _voucherService = voucherService;
        _reciboService = reciboService;
        _voucherRepo = voucherRepo;
        _reciboRepo = reciboRepo;
        _pdf = pdf;
        _dialog = dialog;

        CargarCommand = new AsyncRelayCommand(CargarAsync);
        // Acción por agencia sobre los vouchers libres: 1ª emisión o consolidado complementario.
        EmitirReciboClienteCommand = new AsyncRelayCommand(
            p => EmitirReciboClienteAsync(p as ClienteCierrePeriodoVm),
            p => (p as ClienteCierrePeriodoVm)?.Estado == EstadoCierreCliente.Pendiente);
        // Acción por agencia: emite el recibo consolidado y lo envía por mail.
        EmitirYEnviarReciboClienteCommand = new AsyncRelayCommand(
            p => EmitirYEnviarReciboClienteAsync(p as ClienteCierrePeriodoVm),
            p => (p as ClienteCierrePeriodoVm)?.Estado == EstadoCierreCliente.Pendiente);
        PrevisualizarLibresCommand = new AsyncRelayCommand(
            p => PrevisualizarLibresAsync(p as ClienteCierrePeriodoVm),
            p => (p as ClienteCierrePeriodoVm)?.VouchersLibres > 0);
        // Acciones por recibo consolidado (original o complementario).
        PrevisualizarReciboCommand = new AsyncRelayCommand(p => PrevisualizarReciboAsync(p as ConsolidadoCierreVm));
        DescargarReciboCommand = new AsyncRelayCommand(p => DescargarReciboAsync(p as ConsolidadoCierreVm));
        EnviarMailReciboCommand = new AsyncRelayCommand(p => EnviarMailReciboAsync(p as ConsolidadoCierreVm));
        EmitirRecibosCommand = new AsyncRelayCommand(
            EmitirRecibosPeriodoAsync,
            () => Clientes.Any(a => a.Estado == EstadoCierreCliente.Pendiente));
        // Reenvía a todas las agencias con algún consolidado (incluye las ya enviadas).
        EnviarMailsCommand = new AsyncRelayCommand(
            EnviarMailsPeriodoAsync,
            () => Clientes.Any(a => a.TieneConsolidados));
        // Genera+envía los pendientes; si ya está todo generado, reenvía los mails.
        CerrarCommand = new AsyncRelayCommand(
            CerrarPeriodoAsync,
            () => Clientes.Any(a => a.Estado == EstadoCierreCliente.Pendiente || a.TieneConsolidados));

        // Acciones por voucher (facturación individual por voucher).
        VerPdfVoucherCommand = new AsyncRelayCommand(p => VerPdfVoucherAsync(p as VoucherCierreVm), p => p is VoucherCierreVm);
        EmitirVoucherCommand = new AsyncRelayCommand(
            p => EmitirVoucherInternoAsync(p as VoucherCierreVm, enviarMail: false),
            p => (p as VoucherCierreVm)?.PuedeEmitir == true);
        EmitirYEnviarVoucherCommand = new AsyncRelayCommand(
            p => EmitirVoucherInternoAsync(p as VoucherCierreVm, enviarMail: true),
            p => (p as VoucherCierreVm)?.PuedeEmitirYEnviar == true);

        CargarSeguro(CargarAsync);
    }

    private async Task EmitirReciboClienteAsync(ClienteCierrePeriodoVm? agencia)
    {
        if (agencia is not { } ag) return;

        // Tres casos: reintento de un consolidado sin CAE, complementario (ya hay uno emitido), o 1ª emisión.
        var (titulo, mensaje) = ag switch
        {
            { VouchersLibres: 0 } =>
                ("Reintentar emisión",
                 $"Se reintentará la emisión del recibo consolidado pendiente de {ag.ClienteNombre}. El mail NO se enviará aún. ¿Continuar?"),
            { TieneConsolidados: true } =>
                ("Generar recibo complementario",
                 $"{ag.ClienteNombre} ya tiene un consolidado emitido en el período. Se generará un recibo " +
                 $"COMPLEMENTARIO (adicional) por {ag.VouchersLibres} voucher(s) libre(s) ({Formato.Moneda(ag.TotalLibre)}); " +
                 "el recibo anterior queda intacto. El mail NO se enviará aún. ¿Continuar?"),
            _ =>
                ("Generar recibo",
                 $"Se generará el recibo AFIP de {ag.ClienteNombre} ({ag.VouchersLibres} voucher(s), {Formato.Moneda(ag.TotalLibre)}). " +
                 "El mail NO se enviará aún. ¿Continuar?")
        };
        if (!await _dialog.ShowConfirmAsync(titulo, mensaje)) return;

        await EjecutarOcupadoAsync("Emitiendo recibo", async () =>
        {
            var res = await _reciboService.EmitirReciboClienteAsync(ag.ClienteId, Anio, _mes);
            if (!res.Success || res.Data is null) { MostrarError(res.ErrorMessage ?? "No se pudo generar el recibo."); return; }

            var r = res.Data;
            if (!r.Exito) MostrarError(r.ErrorEmision ?? "No se pudo generar el recibo.");
            else MostrarExito($"Recibo de {r.ClienteNombre} emitido en AFIP (Nro. {r.NumeroComprobante}). Pendiente de envío.");
        });

        await CargarAsync();
    }

    private async Task EmitirYEnviarReciboClienteAsync(ClienteCierrePeriodoVm? agencia)
    {
        if (agencia is not { } ag) return;

        // Mismos tres casos que "Emitir", pero además se envía el mail (CerrarPeriodoClienteAsync).
        var (titulo, mensaje) = ag switch
        {
            { VouchersLibres: 0 } =>
                ("Reintentar y enviar",
                 $"Se reintentará la emisión del recibo consolidado pendiente de {ag.ClienteNombre} y se enviará por mail. ¿Continuar?"),
            { TieneConsolidados: true } =>
                ("Generar complementario y enviar",
                 $"{ag.ClienteNombre} ya tiene un consolidado emitido en el período. Se generará un recibo " +
                 $"COMPLEMENTARIO (adicional) por {ag.VouchersLibres} voucher(s) libre(s) ({Formato.Moneda(ag.TotalLibre)}) y se enviará por mail; " +
                 "el recibo anterior queda intacto. ¿Continuar?"),
            _ =>
                ("Generar recibo y enviar",
                 $"Se generará el recibo AFIP de {ag.ClienteNombre} ({ag.VouchersLibres} voucher(s), {Formato.Moneda(ag.TotalLibre)}) y se enviará por mail. ¿Continuar?")
        };
        if (!await _dialog.ShowConfirmAsync(titulo, mensaje)) return;

        await EjecutarOcupadoAsync("Emitiendo y enviando", async () =>
        {
            var res = await _reciboService.CerrarPeriodoClienteAsync(ag.ClienteId, Anio, _mes);
            if (!res.Success || res.Data is null) { MostrarError(res.ErrorMessage ?? "No se pudo generar el recibo."); return; }

            var r = res.Data;
            if (!r.Exito) MostrarError(r.ErrorEmision ?? "No se pudo generar el recibo.");
            else if (r.ErrorMail is not null) MostrarAdvertencia($"Recibo de {r.ClienteNombre} emitido en AFIP (Nro. {r.NumeroComprobante}), pero falló el mail: {r.ErrorMail}");
            else MostrarExito($"Recibo de {r.ClienteNombre} emitido en AFIP (Nro. {r.NumeroComprobante}) y enviado.");
        });

        await CargarAsync();
    }

    private async Task EnviarMailReciboAsync(ConsolidadoCierreVm? consolidado)
    {
        if (consolidado is not { } c) return;
        if (!await _dialog.ShowConfirmAsync("Enviar mail",
                $"Se enviará el PDF del recibo consolidado N° {c.NumeroComprobante}. ¿Continuar?")) return;

        await EjecutarOcupadoAsync("Enviando mail", async () =>
        {
            var res = await _reciboService.ReenviarMailAsync(c.ReciboId);
            if (!res.Success) MostrarError(res.ErrorMessage ?? "No se pudo enviar el mail.");
            else MostrarExito($"Mail enviado correctamente (recibo N° {c.NumeroComprobante}).");
        });

        await CargarAsync();
    }

    // ── Acciones por voucher (facturación individual por voucher) ────────────────────────────────
    private async Task VerPdfVoucherAsync(VoucherCierreVm? voucher)
    {
        if (voucher is not { } v) return;
        byte[]? bytes = null;
        await EjecutarOcupadoAsync("Generando PDF", async () =>
        {
            try
            {
                // Si el voucher ya tiene recibo, mostrar el comprobante fiscal; si está libre, el PDF del voucher.
                if (v.ReciboId is int reciboId)
                {
                    bytes = await GenerarPdfReciboAsync(reciboId);
                }
                else
                {
                    var entidad = await _voucherRepo.GetByIdConDetalleAsync(v.Id);
                    if (entidad is null) { MostrarError("El voucher no se encontró."); return; }
                    bytes = await _pdf.GenerarPdfVoucherAsync(entidad);
                }
            }
            catch (Exception ex) { MostrarError($"No se pudo previsualizar: {ex.Message}"); }
        });
        if (bytes is null) return;
        var titulo = v.ReciboId is not null ? $"Recibo del voucher N° {v.Numero}" : $"Voucher N° {v.Numero}";
        await _dialog.ShowPdfAsync(bytes, titulo, Formato.NombreArchivoSeguro($"{Anio}-{_mes:00} - Voucher {v.Numero}"));
    }

    private async Task EmitirVoucherInternoAsync(VoucherCierreVm? voucher, bool enviarMail)
    {
        if (voucher is not { } v) return;

        var (titulo, mensaje) = (enviarMail, v.Emitido) switch
        {
            (true, true)  => ("Enviar recibo", $"El voucher N° {v.Numero} ya está emitido. Se reenviará el recibo por mail. ¿Continuar?"),
            (true, false) => ("Emitir y enviar", $"Se generará el recibo AFIP del voucher N° {v.Numero} ({Formato.Moneda(v.Importe)}) y se enviará por mail. ¿Continuar?"),
            _             => ("Emitir recibo", $"Se generará el recibo AFIP del voucher N° {v.Numero} ({Formato.Moneda(v.Importe)}). El mail NO se enviará aún. ¿Continuar?")
        };
        if (!await _dialog.ShowConfirmAsync(titulo, mensaje)) return;

        await EjecutarOcupadoAsync(enviarMail ? "Emitiendo y enviando" : "Emitiendo recibo", async () =>
        {
            var res = await _reciboService.EmitirVoucherAsync(v.Id, enviarMail);
            if (!res.Success || res.Data is null) { MostrarError(res.ErrorMessage ?? "No se pudo emitir el recibo del voucher."); return; }

            var r = res.Data;
            if (r.Omitido) MostrarExito(r.ErrorEmision ?? "El voucher ya estaba emitido.");
            else if (!r.Exito) MostrarError(r.ErrorEmision ?? "No se pudo emitir el recibo del voucher.");
            else if (r.ErrorMail is not null) MostrarAdvertencia($"Recibo del voucher N° {v.Numero} emitido (Nro. {r.NumeroComprobante}), pero falló el mail: {r.ErrorMail}");
            else if (v.Emitido && enviarMail) MostrarExito($"Recibo del voucher N° {v.Numero} (Nro. {r.NumeroComprobante}) reenviado por mail.");
            else if (enviarMail) MostrarExito($"Recibo del voucher N° {v.Numero} emitido (Nro. {r.NumeroComprobante}) y enviado.");
            else MostrarExito($"Recibo del voucher N° {v.Numero} emitido en AFIP (Nro. {r.NumeroComprobante}). Pendiente de envío.");
        });

        await CargarAsync();
    }

    private async Task EmitirRecibosPeriodoAsync()
    {
        var pendientes = Clientes.Count(a => a.Estado == EstadoCierreCliente.Pendiente);
        if (pendientes == 0) { MostrarError("No hay agencias pendientes en el período."); return; }

        if (!await _dialog.ShowConfirmAsync("Emitir recibos",
                $"Se generarán los recibos AFIP para {pendientes} agencia(s) pendiente(s) del período {Formato.Periodo(Anio, _mes)}. Los mails NO se enviarán. ¿Continuar?")) return;

        var res = await EjecutarConProgresoAsync("Emitiendo recibos",
            (progreso, ct) => _reciboService.EmitirRecibosPeriodoAsync(Anio, _mes, progreso, ct));
        if (res is not null)
        {
            if (!res.Success || res.Data is null) MostrarError(res.ErrorMessage ?? "No se pudo emitir los recibos.");
            else MostrarResultadoMasivo(res.Data, "Emisión", "Pendientes de envío por mail.");
        }

        await CargarAsync();
    }

    /// <summary>
    /// Resume una operación masiva por agencia: error si todas fallaron, advertencia si fue parcial
    /// o hubo fallos de mail, éxito solo cuando todo salió bien.
    /// </summary>
    private void MostrarResultadoMasivo(IReadOnlyList<ResultadoCierrePorCliente> datos, string accion, string detalleExito)
    {
        if (datos.Count == 0) { MostrarError("No había nada para procesar en el período."); return; }

        var ok = datos.Count(r => r.Exito);
        var fallidos = datos.Where(r => !r.Exito).ToList();
        var primerError = fallidos.FirstOrDefault()?.ErrorEmision ?? "Error desconocido.";
        var primerErrorMail = datos.FirstOrDefault(r => r.Exito && r.ErrorMail is not null)?.ErrorMail;

        if (ok == 0)
            MostrarError($"{accion} fallida: ningún recibo se generó en AFIP ({fallidos.Count} agencia(s) con error). {primerError}");
        else if (fallidos.Count > 0)
            MostrarAdvertencia($"{accion} parcial: {ok} recibo(s) ok, {fallidos.Count} con error. Primer error: {primerError}");
        else if (primerErrorMail is not null)
            MostrarAdvertencia($"{accion} finalizada: {ok} recibo(s) generado(s), pero hubo errores de mail. {primerErrorMail}");
        else
            MostrarExito($"{accion} finalizada: {ok} recibo(s) generado(s) en AFIP. {detalleExito}");
    }

    private async Task EnviarMailsPeriodoAsync()
    {
        var recibos = RecibosDelPeriodo();
        if (recibos.Count == 0) { MostrarError("No hay recibos generados para enviar."); return; }

        if (!await _dialog.ShowConfirmAsync("Enviar mails",
                $"Se enviará (o reenviará) el mail con el PDF consolidado de {recibos.Count} recibo(s) del período {Formato.Periodo(Anio, _mes)}. ¿Continuar?")) return;

        await ReenviarMailsAsync(recibos);
        await CargarAsync();
    }

    /// <summary>Todos los recibos consolidados del período (original + complementarios), con el nombre de su agencia.</summary>
    private IReadOnlyList<(string Nombre, int ReciboId)> RecibosDelPeriodo()
        => Clientes.SelectMany(a => a.Consolidados.Select(c => (a.ClienteNombre, c.ReciboId))).ToList();

    /// <summary>Reenvía el mail de cada recibo (siempre, sin importar si ya fue enviado) y resume el resultado.</summary>
    private Task ReenviarMailsAsync(IReadOnlyList<(string Nombre, int ReciboId)> recibos)
        => EjecutarConProgresoAsync("Enviando mails", async (progreso, ct) =>
        {
            int ok = 0;
            var errores = new List<string>();
            var total = recibos.Count;
            var i = 0;
            foreach (var (nombre, reciboId) in recibos)
            {
                ct.ThrowIfCancellationRequested();
                progreso.Report(new ProgresoMasivo(++i, total, nombre));
                var res = await _reciboService.ReenviarMailAsync(reciboId, ct);
                if (res.Success) ok++;
                else errores.Add($"{nombre}: {res.ErrorMessage ?? "error desconocido"}");
            }

            if (errores.Count == 0) MostrarExito($"Mails enviados correctamente: {ok} recibo(s).");
            else if (ok == 0) MostrarError($"No se pudo enviar ningún mail ({errores.Count} con error). {errores[0]}");
            else MostrarAdvertencia($"Envío parcial: {ok} correctos, {errores.Count} con error. Primer error: {errores[0]}");
        });

    private async Task CerrarPeriodoAsync()
    {
        var pendientes = Clientes.Count(a => a.Estado == EstadoCierreCliente.Pendiente);

        // Con agencias pendientes: generar+enviar solo esas (no reenvía las ya enviadas).
        if (pendientes > 0)
        {
            if (!await _dialog.ShowConfirmAsync("Cerrar período",
                    $"Se generará un recibo consolidado para {pendientes} agencia(s) pendiente(s) del período {Formato.Periodo(Anio, _mes)} y se enviarán por mail. ¿Continuar?")) return;

            var res = await EjecutarConProgresoAsync("Cerrando período",
                (progreso, ct) => _reciboService.CerrarPeriodoAsync(Anio, _mes, progreso, ct));
            if (res is not null)
            {
                if (!res.Success || res.Data is null) MostrarError(res.ErrorMessage ?? "No se pudo cerrar el período.");
                else MostrarResultadoMasivo(res.Data, "Cierre", "Mails enviados.");
            }

            await CargarAsync();
            return;
        }

        // Todo el período ya está generado → reenviar el mail de todos los recibos.
        var recibos = RecibosDelPeriodo();
        if (recibos.Count == 0) { MostrarError("No hay agencias con vouchers ni recibos en el período."); return; }

        if (!await _dialog.ShowConfirmAsync("Reenviar mails",
                $"El período ya está cerrado: todos los recibos están generados. Se reenviará el mail con el PDF consolidado de {recibos.Count} recibo(s). ¿Continuar?")) return;

        await ReenviarMailsAsync(recibos);
        await CargarAsync();
    }

    private async Task CargarAsync()
    {
        LimpiarStatus();
        await EjecutarOcupadoAsync("Cargando", async () =>
        {
            var res = await _voucherService.GetCierrePeriodoAsync(Anio, _mes);
            Clientes.Clear();
            if (!res.Success || res.Data is null)
            {
                MostrarError(res.ErrorMessage ?? "No se pudo consultar.");
                Resumen = string.Empty;
                TotalPeriodo = 0;
                return;
            }

            foreach (var a in res.Data) Clientes.Add(a);
            TotalPeriodo = Clientes.Sum(a => a.Total);
            Resumen = ArmarResumen(Clientes);
        });
    }

    private static string ArmarResumen(IEnumerable<ClienteCierrePeriodoVm> agencias)
    {
        var lista = agencias.ToList();
        if (lista.Count == 0) return "Sin vouchers en el período.";
        var pendientes = lista.Count(a => a.Estado == EstadoCierreCliente.Pendiente);
        var emitidos   = lista.Count(a => a.Estado == EstadoCierreCliente.Emitido);
        var completos  = lista.Count(a => a.Estado == EstadoCierreCliente.Completo);
        var totalV     = lista.Sum(a => a.Vouchers.Count);
        return $"{lista.Count} agencia(s) · {totalV} voucher(s) · {pendientes} pendiente(s), {emitidos} emitido(s), {completos} completo(s)";
    }

    // ── PDF: vouchers libres (lo que se va a emitir) ─────────────────────────────────────────────
    private async Task PrevisualizarLibresAsync(ClienteCierrePeriodoVm? agencia)
    {
        if (agencia is not { } ag) return;
        byte[]? bytes = null;
        await EjecutarOcupadoAsync("Generando PDF", async () =>
        {
            try { bytes = await GenerarPdfLibresAsync(ag.ClienteId); }
            catch (Exception ex) { MostrarError($"No se pudo previsualizar: {ex.Message}"); }
        });
        if (bytes is null) return;
        await _dialog.ShowPdfAsync(bytes, $"Vouchers libres — {ag.ClienteNombre}", $"{NombreArchivoCliente(ag)} - vouchers libres");
    }

    // ── PDF: recibo consolidado (original o complementario) ──────────────────────────────────────
    private async Task DescargarReciboAsync(ConsolidadoCierreVm? consolidado)
    {
        if (consolidado is not { } c) return;
        var ag = ClienteDe(c);
        var dlg = new SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = $"{NombreArchivoRecibo(ag, c)}.pdf"
        };
        if (dlg.ShowDialog() != true) return;

        await EjecutarOcupadoAsync("Generando PDF", async () =>
        {
            try
            {
                var bytes = await GenerarPdfReciboAsync(c.ReciboId);
                if (bytes is null) return;
                await File.WriteAllBytesAsync(dlg.FileName, bytes);
                MostrarExito($"PDF generado: {Path.GetFileName(dlg.FileName)}");
            }
            catch (Exception ex) { MostrarError($"No se pudo generar el PDF: {ex.Message}"); }
        });
    }

    private async Task PrevisualizarReciboAsync(ConsolidadoCierreVm? consolidado)
    {
        if (consolidado is not { } c) return;
        var ag = ClienteDe(c);
        byte[]? bytes = null;
        await EjecutarOcupadoAsync("Generando PDF", async () =>
        {
            try { bytes = await GenerarPdfReciboAsync(c.ReciboId); }
            catch (Exception ex) { MostrarError($"No se pudo previsualizar: {ex.Message}"); }
        });
        if (bytes is null) return;
        await _dialog.ShowPdfAsync(bytes, $"Recibo consolidado N° {c.NumeroComprobante}", NombreArchivoRecibo(ag, c));
    }

    private ClienteCierrePeriodoVm? ClienteDe(ConsolidadoCierreVm consolidado)
        => Clientes.FirstOrDefault(a => a.Consolidados.Contains(consolidado));

    private string NombreArchivoCliente(ClienteCierrePeriodoVm agencia)
        => Formato.NombreArchivoSeguro($"{Anio}-{_mes:00} - {agencia.ClienteNombre}");

    private string NombreArchivoRecibo(ClienteCierrePeriodoVm? agencia, ConsolidadoCierreVm consolidado)
        => Formato.NombreArchivoSeguro($"{Anio}-{_mes:00} - {agencia?.ClienteNombre ?? "Recibo"} - N {consolidado.NumeroComprobante}");

    private async Task<byte[]?> GenerarPdfReciboAsync(int reciboId)
    {
        var recibo = await _reciboRepo.GetConDetalleAsync(reciboId);
        if (recibo is null) { MostrarError("El recibo no se encontró."); return null; }
        var consolidacion = await _reciboRepo.GetConsolidacionByReciboAsync(reciboId);
        var vouchers = consolidacion?.Vouchers.OrderBy(v => v.Numero).ToList() ?? [];
        return await _pdf.GenerarPdfDescargaAsync(vouchers, recibo);
    }

    private async Task<byte[]?> GenerarPdfLibresAsync(int agenciaId)
    {
        var vouchers = await CargarVouchersLibresAsync(agenciaId);
        if (vouchers.Count == 0) { MostrarError("La agencia no tiene vouchers libres en el período."); return null; }
        return await _pdf.GenerarPdfDescargaAsync(vouchers, recibo: null);
    }

    private async Task<IReadOnlyList<Voucher>> CargarVouchersLibresAsync(int agenciaId)
    {
        var todos = await _voucherRepo.GetPorClienteAsync(agenciaId, Anio, _mes);
        return todos.Where(v => v.ConsolidacionId is null).ToList();
    }
}
