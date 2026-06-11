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

    public ObservableCollection<AgenciaCierrePeriodoVm> Agencias { get; } = [];

    public IReadOnlyList<string> MesesNombres { get; } =
        Enumerable.Range(1, 12)
            .Select(m => _es.DateTimeFormat.GetMonthName(m))
            .Select(n => char.ToUpper(n[0]) + n[1..])
            .ToList();

    private int _anio = DateTime.Today.Year;
    public int Anio { get => _anio; set { if (SetField(ref _anio, value)) _ = CargarAsync(); } }

    private int _mesIndex = DateTime.Today.Month - 1;
    public int MesIndex
    {
        get => _mesIndex;
        set { if (SetField(ref _mesIndex, value)) { _mes = value + 1; _ = CargarAsync(); } }
    }

    private int _mes = DateTime.Today.Month;

    private string _resumen = string.Empty;
    public string Resumen { get => _resumen; set => SetField(ref _resumen, value); }

    private decimal _totalPeriodo;
    public decimal TotalPeriodo { get => _totalPeriodo; set { SetField(ref _totalPeriodo, value); OnPropertyChanged(nameof(TotalPeriodoTexto)); } }
    public string TotalPeriodoTexto => Formato.Moneda(_totalPeriodo);

    public ICommand CargarCommand { get; }
    public ICommand DescargarPdfCommand { get; }
    public ICommand PrevisualizarPdfCommand { get; }
    public ICommand EmitirReciboAgenciaCommand { get; }
    public ICommand EnviarMailAgenciaCommand { get; }
    public ICommand EmitirRecibosCommand { get; }
    public ICommand EnviarMailsCommand { get; }
    public ICommand CerrarCommand { get; }

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
        DescargarPdfCommand = new AsyncRelayCommand(p => DescargarPdfAsync(p as AgenciaCierrePeriodoVm));
        PrevisualizarPdfCommand = new AsyncRelayCommand(p => PrevisualizarPdfAsync(p as AgenciaCierrePeriodoVm));
        EmitirReciboAgenciaCommand = new AsyncRelayCommand(
            p => EmitirReciboAgenciaAsync(p as AgenciaCierrePeriodoVm),
            p => (p as AgenciaCierrePeriodoVm)?.Estado == EstadoCierreAgencia.Pendiente);
        EnviarMailAgenciaCommand = new AsyncRelayCommand(
            p => EnviarMailAgenciaAsync(p as AgenciaCierrePeriodoVm),
            p => (p as AgenciaCierrePeriodoVm)?.Estado == EstadoCierreAgencia.Emitido
                 && (p as AgenciaCierrePeriodoVm)?.ReciboId is not null);
        EmitirRecibosCommand = new AsyncRelayCommand(
            EmitirRecibosPeriodoAsync,
            () => Agencias.Any(a => a.Estado == EstadoCierreAgencia.Pendiente));
        EnviarMailsCommand = new AsyncRelayCommand(
            EnviarMailsPeriodoAsync,
            () => Agencias.Any(a => a.Estado == EstadoCierreAgencia.Emitido && a.ReciboId is not null));
        CerrarCommand = new AsyncRelayCommand(
            CerrarPeriodoAsync,
            () => Agencias.Any(a => a.Estado == EstadoCierreAgencia.Pendiente));

        _ = CargarAsync();
    }

    private async Task EmitirReciboAgenciaAsync(AgenciaCierrePeriodoVm? agencia)
    {
        if (agencia is null) return;
        if (!await _dialog.ShowConfirmAsync("Generar recibo",
                $"Se generará el recibo AFIP de {agencia.AgenciaNombre} ({agencia.Vouchers.Count} voucher(s), {Formato.Moneda(agencia.Total)}). El mail NO se enviará aún. ¿Continuar?")) return;

        IsBusy = true;
        try
        {
            var res = await _reciboService.EmitirReciboAgenciaAsync(agencia.AgenciaId, Anio, _mes);
            if (!res.Success || res.Data is null) { MostrarError(res.ErrorMessage ?? "No se pudo generar el recibo."); return; }

            var r = res.Data;
            if (!r.Exito) MostrarError(r.ErrorEmision ?? "No se pudo generar el recibo.");
            else MostrarExito($"Recibo de {r.AgenciaNombre} emitido en AFIP (Nro. {r.NumeroComprobante}). Pendiente de envío.");
        }
        finally { IsBusy = false; }

        await CargarAsync();
    }

    private async Task EnviarMailAgenciaAsync(AgenciaCierrePeriodoVm? agencia)
    {
        if (agencia is null || agencia.ReciboId is null) return;
        if (!await _dialog.ShowConfirmAsync("Enviar mail",
                $"Se enviará el PDF del recibo consolidado de {agencia.AgenciaNombre}. ¿Continuar?")) return;

        IsBusy = true;
        try
        {
            var res = await _reciboService.ReenviarMailAsync(agencia.ReciboId.Value);
            if (!res.Success) MostrarError(res.ErrorMessage ?? "No se pudo enviar el mail.");
            else MostrarExito($"Mail enviado correctamente para {agencia.AgenciaNombre}.");
        }
        finally { IsBusy = false; }

        await CargarAsync();
    }

    private async Task EmitirRecibosPeriodoAsync()
    {
        var pendientes = Agencias.Count(a => a.Estado == EstadoCierreAgencia.Pendiente);
        if (pendientes == 0) { MostrarError("No hay agencias pendientes en el período."); return; }

        if (!await _dialog.ShowConfirmAsync("Emitir recibos",
                $"Se generarán los recibos AFIP para {pendientes} agencia(s) pendiente(s) del período {Formato.Periodo(Anio, _mes)}. Los mails NO se enviarán. ¿Continuar?")) return;

        IsBusy = true;
        try
        {
            var res = await _reciboService.EmitirRecibosPeriodoAsync(Anio, _mes);
            if (!res.Success || res.Data is null) { MostrarError(res.ErrorMessage ?? "No se pudo emitir los recibos."); return; }

            MostrarResultadoMasivo(res.Data, "Emisión", "Pendientes de envío por mail.");
        }
        finally { IsBusy = false; }

        await CargarAsync();
    }

    /// <summary>
    /// Resume una operación masiva por agencia: error si todas fallaron, advertencia si fue parcial
    /// o hubo fallos de mail, éxito solo cuando todo salió bien.
    /// </summary>
    private void MostrarResultadoMasivo(IReadOnlyList<ResultadoCierrePorAgencia> datos, string accion, string detalleExito)
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
        var conRecibo = Agencias.Where(a => a.Estado == EstadoCierreAgencia.Emitido && a.ReciboId is not null).ToList();
        if (conRecibo.Count == 0) { MostrarError("No hay recibos emitidos pendientes de envío."); return; }

        if (!await _dialog.ShowConfirmAsync("Enviar mails",
                $"Se enviará el mail con el PDF consolidado a {conRecibo.Count} agencia(s) con recibo emitido del período {Formato.Periodo(Anio, _mes)}. ¿Continuar?")) return;

        IsBusy = true;
        int ok = 0;
        var errores = new List<string>();
        try
        {
            foreach (var agencia in conRecibo)
            {
                var res = await _reciboService.ReenviarMailAsync(agencia.ReciboId!.Value);
                if (res.Success) ok++;
                else errores.Add($"{agencia.AgenciaNombre}: {res.ErrorMessage ?? "error desconocido"}");
            }

            if (errores.Count == 0) MostrarExito($"Mails enviados correctamente: {ok} agencia(s).");
            else if (ok == 0) MostrarError($"No se pudo enviar ningún mail ({errores.Count} con error). {errores[0]}");
            else MostrarAdvertencia($"Envío parcial: {ok} correctos, {errores.Count} con error. Primer error: {errores[0]}");
        }
        finally { IsBusy = false; }

        await CargarAsync();
    }

    private async Task CerrarPeriodoAsync()
    {
        var pendientes = Agencias.Count(a => a.Estado == EstadoCierreAgencia.Pendiente);
        if (pendientes == 0) { MostrarError("No hay agencias pendientes en el período."); return; }

        if (!await _dialog.ShowConfirmAsync("Cerrar período",
                $"Se generará un recibo consolidado para {pendientes} agencia(s) pendiente(s) del período {Formato.Periodo(Anio, _mes)} y se enviarán por mail. ¿Continuar?")) return;

        IsBusy = true;
        try
        {
            var res = await _reciboService.CerrarPeriodoAsync(Anio, _mes);
            if (!res.Success || res.Data is null) { MostrarError(res.ErrorMessage ?? "No se pudo cerrar el período."); return; }

            MostrarResultadoMasivo(res.Data, "Cierre", "Mails enviados.");
        }
        finally { IsBusy = false; }

        await CargarAsync();
    }

    private async Task CargarAsync()
    {
        LimpiarStatus();
        IsBusy = true;
        try
        {
            var res = await _voucherService.GetCierrePeriodoAsync(Anio, _mes);
            Agencias.Clear();
            if (!res.Success || res.Data is null)
            {
                MostrarError(res.ErrorMessage ?? "No se pudo consultar.");
                Resumen = string.Empty;
                TotalPeriodo = 0;
                return;
            }

            foreach (var a in res.Data) Agencias.Add(a);
            TotalPeriodo = Agencias.Sum(a => a.Total);
            Resumen = ArmarResumen(Agencias);
        }
        finally { IsBusy = false; }
    }

    private static string ArmarResumen(IEnumerable<AgenciaCierrePeriodoVm> agencias)
    {
        var lista = agencias.ToList();
        if (lista.Count == 0) return "Sin vouchers en el período.";
        var pendientes = lista.Count(a => a.Estado == EstadoCierreAgencia.Pendiente);
        var emitidos   = lista.Count(a => a.Estado == EstadoCierreAgencia.Emitido);
        var completos  = lista.Count(a => a.Estado == EstadoCierreAgencia.Completo);
        var totalV     = lista.Sum(a => a.Vouchers.Count);
        return $"{lista.Count} agencia(s) · {totalV} voucher(s) · {pendientes} pendiente(s), {emitidos} emitido(s), {completos} completo(s)";
    }

    private async Task DescargarPdfAsync(AgenciaCierrePeriodoVm? agencia)
    {
        if (agencia is null) return;

        var dlg = new SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = $"{NombreArchivoAgencia(agencia)}.pdf"
        };
        if (dlg.ShowDialog() != true) return;

        IsBusy = true;
        try
        {
            var bytes = await GenerarPdfAgenciaAsync(agencia);
            if (bytes is null) return;

            await File.WriteAllBytesAsync(dlg.FileName, bytes);
            MostrarExito($"PDF generado: {Path.GetFileName(dlg.FileName)}");
        }
        catch (Exception ex)
        {
            MostrarError($"No se pudo generar el PDF: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    private async Task PrevisualizarPdfAsync(AgenciaCierrePeriodoVm? agencia)
    {
        if (agencia is null) return;

        byte[]? bytes;
        IsBusy = true;
        try { bytes = await GenerarPdfAgenciaAsync(agencia); }
        catch (Exception ex) { MostrarError($"No se pudo previsualizar: {ex.Message}"); return; }
        finally { IsBusy = false; }

        if (bytes is null) return;
        var titulo = agencia.ReciboId is not null
            ? $"Recibo consolidado — {agencia.AgenciaNombre}"
            : $"Vouchers — {agencia.AgenciaNombre}";
        await _dialog.ShowPdfAsync(bytes, titulo, NombreArchivoAgencia(agencia));
    }

    private string NombreArchivoAgencia(AgenciaCierrePeriodoVm agencia)
        => Formato.NombreArchivoSeguro($"{Anio}-{_mes:00} - {agencia.AgenciaNombre}");

    private async Task<byte[]?> GenerarPdfAgenciaAsync(AgenciaCierrePeriodoVm agencia)
    {
        if (agencia.ReciboId is not null)
        {
            var recibo = await _reciboRepo.GetConDetalleAsync(agencia.ReciboId.Value);
            if (recibo is null) { MostrarError("El recibo no se encontró."); return null; }
            return await _pdf.GenerarPdfDescargaAsync(recibo.Vouchers.ToList(), recibo);
        }

        var vouchers = await CargarVouchersPendientesAsync(agencia.AgenciaId);
        if (vouchers.Count == 0) { MostrarError("La agencia no tiene vouchers en el período."); return null; }
        return await _pdf.GenerarPdfDescargaAsync(vouchers, recibo: null);
    }

    private async Task<IReadOnlyList<Voucher>> CargarVouchersPendientesAsync(int agenciaId)
    {
        var todos = await _voucherRepo.GetPorAgenciaAsync(agenciaId, Anio, _mes);
        return todos.Where(v => v.ReciboId is null).ToList();
    }
}
