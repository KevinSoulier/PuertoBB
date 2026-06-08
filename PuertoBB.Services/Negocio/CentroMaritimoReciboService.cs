using Microsoft.Extensions.Logging;
using PuertoBB.Core.Common;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Enums;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Afip;
using PuertoBB.Core.Models.Resultados;
using PuertoBB.Services.Common;

namespace PuertoBB.Services.Negocio;

public class CentroMaritimoReciboService : ICentroMaritimoReciboService
{
    private readonly IReciboRepository _recibos;
    private readonly IGrupoFacturacionRepository _grupos;
    private readonly IAgenciaRepository _agencias;
    private readonly IVoucherRepository _vouchers;
    private readonly INotaDeCreditoRepository _notas;
    private readonly IConfiguracionRepository _config;
    private readonly IAfipService _afip;
    private readonly ICentroMaritimoPdfService _pdf;
    private readonly IMailService _mail;
    private readonly ILogger<CentroMaritimoReciboService> _logger;

    public CentroMaritimoReciboService(
        IReciboRepository recibos,
        IGrupoFacturacionRepository grupos,
        IAgenciaRepository agencias,
        IVoucherRepository vouchers,
        INotaDeCreditoRepository notas,
        IConfiguracionRepository config,
        IAfipService afip,
        ICentroMaritimoPdfService pdf,
        IMailService mail,
        ILogger<CentroMaritimoReciboService> logger)
    {
        _recibos = recibos;
        _grupos = grupos;
        _agencias = agencias;
        _vouchers = vouchers;
        _notas = notas;
        _config = config;
        _afip = afip;
        _pdf = pdf;
        _mail = mail;
        _logger = logger;
    }

    public async Task<ServiceResult<IReadOnlyList<ResultadoCierrePorAgencia>>> CerrarPeriodoAsync(int anio, int mes, CancellationToken ct = default)
    {
        var pendientes = await _vouchers.GetPendientesByPeriodoAsync(anio, mes, ct);
        var config = await _config.GetAsync(ct);
        var porAgencia = pendientes.GroupBy(v => v.AgenciaId).ToList();
        var resultados = new List<ResultadoCierrePorAgencia>();

        foreach (var grupo in porAgencia)
        {
            ct.ThrowIfCancellationRequested();
            resultados.Add(await ProcesarCierreAgenciaAsync(grupo.Key, grupo.OrderBy(v => v.Numero).ToList(), anio, mes, config, enviarMail: true, ct));
        }

        return ServiceResult<IReadOnlyList<ResultadoCierrePorAgencia>>.Ok(resultados);
    }

    public async Task<ServiceResult<ResultadoCierrePorAgencia>> CerrarPeriodoAgenciaAsync(int agenciaId, int anio, int mes, CancellationToken ct = default)
    {
        var pendientes = await _vouchers.GetPendientesByPeriodoAsync(anio, mes, ct);
        var vouchersAgencia = pendientes.Where(v => v.AgenciaId == agenciaId).OrderBy(v => v.Numero).ToList();
        if (vouchersAgencia.Count == 0)
            return ServiceResult<ResultadoCierrePorAgencia>.Fail("La agencia no tiene vouchers pendientes en el período.");

        var config = await _config.GetAsync(ct);
        var resultado = await ProcesarCierreAgenciaAsync(agenciaId, vouchersAgencia, anio, mes, config, enviarMail: true, ct);
        return resultado.Exito
            ? ServiceResult<ResultadoCierrePorAgencia>.Ok(resultado)
            : ServiceResult<ResultadoCierrePorAgencia>.Fail(resultado.ErrorEmision ?? "No se pudo cerrar el período de la agencia.");
    }

    /// <summary>
    /// Consolida los vouchers (ya cargados, con Agencia/Barco) de una agencia en un recibo:
    /// valida duplicado, obtiene CAE, persiste, marca vouchers y envía el PDF único por mail.
    /// </summary>
    public async Task<ServiceResult<ResultadoCierrePorAgencia>> EmitirReciboAgenciaAsync(int agenciaId, int anio, int mes, CancellationToken ct = default)
    {
        var pendientes = await _vouchers.GetPendientesByPeriodoAsync(anio, mes, ct);
        var vouchersAgencia = pendientes.Where(v => v.AgenciaId == agenciaId).OrderBy(v => v.Numero).ToList();
        if (vouchersAgencia.Count == 0)
            return ServiceResult<ResultadoCierrePorAgencia>.Fail("La agencia no tiene vouchers pendientes en el período.");

        var config = await _config.GetAsync(ct);
        var resultado = await ProcesarCierreAgenciaAsync(agenciaId, vouchersAgencia, anio, mes, config, enviarMail: false, ct);
        return resultado.Exito
            ? ServiceResult<ResultadoCierrePorAgencia>.Ok(resultado)
            : ServiceResult<ResultadoCierrePorAgencia>.Fail(resultado.ErrorEmision ?? "No se pudo emitir el recibo.");
    }

    public async Task<ServiceResult<IReadOnlyList<ResultadoCierrePorAgencia>>> EmitirRecibosPeriodoAsync(int anio, int mes, CancellationToken ct = default)
    {
        var pendientes = await _vouchers.GetPendientesByPeriodoAsync(anio, mes, ct);
        var config = await _config.GetAsync(ct);
        var porAgencia = pendientes.GroupBy(v => v.AgenciaId).ToList();
        var resultados = new List<ResultadoCierrePorAgencia>();

        foreach (var grupo in porAgencia)
        {
            ct.ThrowIfCancellationRequested();
            resultados.Add(await ProcesarCierreAgenciaAsync(grupo.Key, grupo.OrderBy(v => v.Numero).ToList(), anio, mes, config, enviarMail: false, ct));
        }

        return ServiceResult<IReadOnlyList<ResultadoCierrePorAgencia>>.Ok(resultados);
    }

    private async Task<ResultadoCierrePorAgencia> ProcesarCierreAgenciaAsync(
        int agenciaId, IReadOnlyList<Voucher> vouchersAgencia, int anio, int mes, Configuracion config, bool enviarMail, CancellationToken ct)
    {
        var nombreAgencia = vouchersAgencia.FirstOrDefault()?.Agencia?.Nombre ?? $"#{agenciaId}";
        try
        {
            if (await _recibos.ExisteConsolidadoAsync(agenciaId, anio, mes, ct))
                return ResultadoCierrePorAgencia.Omitida(agenciaId, nombreAgencia, "Ya existe un recibo consolidado para este período.");

            // Cargar la agencia rastreada (con emails) para evitar reinsertar entidades detached al persistir.
            var agencia = await _agencias.GetConDetalleAsync(agenciaId, ct);
            if (agencia is null)
                return ResultadoCierrePorAgencia.Fallo(agenciaId, nombreAgencia, "La agencia no existe.");

            var importe = vouchersAgencia.Sum(v => v.Importe);
            var detalle = "Vouchers Nros: " + string.Join(", ", vouchersAgencia.Select(v => v.Numero));

            var recibo = ConstruirRecibo(agencia, null, importe, detalle, anio, mes, config, esConsolidado: true);

            var cae = await EmitirCaeAsync(recibo, agencia, config, ct);
            if (!cae.Success || cae.Data is null)
                return ResultadoCierrePorAgencia.Fallo(agencia.Id, agencia.Nombre, cae.ErrorMessage ?? "AFIP no devolvió CAE.");
            AplicarCae(recibo, cae.Data);

            await _recibos.AddAsync(recibo, ct);
            await _vouchers.MarcarConsolidadosAsync(vouchersAgencia.Select(v => v.Id), recibo.Id, ct);

            string? errorMail = null;
            if (enviarMail)
            {
                // Recargar con la agencia rastreada por el contexto de _recibos para que el PDF tenga
                // los datos del receptor y el UpdateAsync no reinserte la agencia (DbContext transient).
                var reciboParaEnvio = await _recibos.GetConDetalleAsync(recibo.Id, ct) ?? recibo;
                errorMail = await EnviarConsolidadoAsync(reciboParaEnvio, vouchersAgencia, agencia, anio, mes, ct);
                if (errorMail is null)
                {
                    reciboParaEnvio.Estado = ReciboEstado.Enviado;
                    await _recibos.UpdateAsync(reciboParaEnvio, ct);
                }
            }

            return ResultadoCierrePorAgencia.Ok(agencia.Id, agencia.Nombre, vouchersAgencia.Count, importe, recibo.NumeroComprobante, errorMail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cerrar período para Agencia={AgenciaId}", agenciaId);
            return ResultadoCierrePorAgencia.Fallo(agenciaId, nombreAgencia, ex.Message);
        }
    }

    public async Task<ServiceResult<IReadOnlyList<string>>> GetDuplicadosAsync(int grupoId, int anio, int mes, CancellationToken ct = default)
    {
        var grupo = await _grupos.GetConMiembrosAsync(grupoId, ct);
        if (grupo is null) return ServiceResult<IReadOnlyList<string>>.Fail("El grupo no existe.");

        var dup = new List<string>();
        foreach (var ag in grupo.Agencias)
            if (await _recibos.ExisteAsync(ag.AgenciaId, grupoId, anio, mes, ct))
                dup.Add(ag.Agencia.Nombre);

        return ServiceResult<IReadOnlyList<string>>.Ok(dup);
    }

    public async Task<ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>> EmitirMasivoAsync(int grupoId, int anio, int mes, CancellationToken ct = default)
    {
        var grupo = await _grupos.GetConMiembrosAsync(grupoId, ct);
        if (grupo is null) return ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>.Fail("El grupo no existe.");

        var config = await _config.GetAsync(ct);
        var resultados = new List<ResultadoEmisionPorEntidad>();

        foreach (var ag in grupo.Agencias)
        {
            ct.ThrowIfCancellationRequested();
            resultados.Add(await EmitirOResumirAsync(ag.Agencia, grupoId, grupo.Importe, grupo.Nombre, DateTime.Today, anio, mes, config, enviarMail: true, ct));
        }

        return ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>.Ok(resultados);
    }

    public async Task<ServiceResult<ResultadoEmisionPorEntidad>> EmitirIndividualAsync(int agenciaId, decimal importe, string detalle, DateTime fechaEmision, int anio, int mes, bool enviarMail, CancellationToken ct = default)
    {
        var agencia = await _agencias.GetConDetalleAsync(agenciaId, ct);
        if (agencia is null) return ServiceResult<ResultadoEmisionPorEntidad>.Fail("La agencia no existe.");

        var config = await _config.GetAsync(ct);
        return ServiceResult<ResultadoEmisionPorEntidad>.Ok(await EmitirOResumirAsync(agencia, null, importe, detalle, fechaEmision, anio, mes, config, enviarMail, ct));
    }

    public async Task<ServiceResult<ResultadoEmisionPorEntidad>> ReintentarAsync(int reciboId, bool enviarMail, CancellationToken ct = default)
    {
        var recibo = await _recibos.GetConDetalleAsync(reciboId, ct);
        if (recibo is null) return ServiceResult<ResultadoEmisionPorEntidad>.Fail("El recibo no existe.");
        if (recibo.Estado is ReciboEstado.Pagado or ReciboEstado.Anulado)
            return ServiceResult<ResultadoEmisionPorEntidad>.Fail("El recibo no admite reintento.");

        var config = await _config.GetAsync(ct);
        var resultado = await ProcesarReciboAsync(recibo, recibo.Agencia, config, enviarMail, ct);
        return ServiceResult<ResultadoEmisionPorEntidad>.Ok(resultado);
    }

    /// <summary>
    /// Crea (en estado Pendiente, persistido antes del CAE) o resume un recibo de cuota del período, y
    /// avanza su emisión idempotentemente. Si ya está completo, devuelve "ya existe".
    /// </summary>
    private async Task<ResultadoEmisionPorEntidad> EmitirOResumirAsync(
        Agencia agencia, int? grupoId, decimal importe, string detalle, DateTime fechaEmision, int anio, int mes, Configuracion config, bool enviarMail, CancellationToken ct)
    {
        try
        {
            var recibo = await _recibos.GetPorClaveAsync(agencia.Id, grupoId, anio, mes, ct);
            if (recibo is null)
            {
                recibo = ConstruirReciboPendiente(agencia, grupoId, importe, detalle, fechaEmision, anio, mes, config);
                await _recibos.AddAsync(recibo, ct);
            }
            else
            {
                agencia = recibo.Agencia; // rastreada, con emails
                if (EsCompleto(recibo))
                    return ResultadoEmisionPorEntidad.Fallo(agencia.Id, agencia.Nombre, "Ya existe un recibo para este período.");
            }

            return await ProcesarReciboAsync(recibo, agencia, config, enviarMail, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al emitir cuota para Agencia={AgenciaId}", agencia.Id);
            return ResultadoEmisionPorEntidad.Fallo(agencia.Id, agencia.Nombre, ex.Message);
        }
    }

    /// <summary>Un recibo está "completo" cuando ya no hay nada que reintentar.</summary>
    private static bool EsCompleto(Recibo r)
        => r.Estado is ReciboEstado.Enviado or ReciboEstado.Pagado or ReciboEstado.Anulado
           || (r.Estado == ReciboEstado.Emitido && r.UltimoErrorMail is null);

    /// <summary>
    /// Avanza un recibo persistido: pide el CAE solo si falta (idempotente), y manda el mail si
    /// corresponde. Para recibos consolidados arma el PDF de descarga; para cuotas, el recibo simple.
    /// </summary>
    private async Task<ResultadoEmisionPorEntidad> ProcesarReciboAsync(
        Recibo recibo, Agencia agencia, Configuracion config, bool enviarMail, CancellationToken ct)
    {
        // 1. CAE (idempotente: no se vuelve a pedir si ya está).
        if (string.IsNullOrEmpty(recibo.CAE))
        {
            var cae = await EmitirCaeAsync(recibo, agencia, config, ct);
            if (!cae.Success || cae.Data is null)
            {
                recibo.Estado = ReciboEstado.Pendiente;
                recibo.UltimoErrorCae = cae.ErrorMessage ?? "AFIP no devolvió CAE.";
                await _recibos.UpdateAsync(recibo, ct);
                return ResultadoEmisionPorEntidad.Fallo(agencia.Id, agencia.Nombre, recibo.UltimoErrorCae);
            }
            AplicarCae(recibo, cae.Data);
            recibo.UltimoErrorCae = null;
            recibo.Estado = ReciboEstado.Emitido;
            await _recibos.UpdateAsync(recibo, ct);
        }

        // 2. Mail (opcional; un fallo NO revierte la emisión).
        if (enviarMail && recibo.Estado != ReciboEstado.Enviado)
        {
            // Un recibo recién creado no tiene la navegación Agencia cargada (solo el FK). Recargarlo
            // desde el contexto de _recibos para que el PDF tenga los datos del receptor. GetConDetalle
            // resuelve a la MISMA instancia rastreada, así que las actualizaciones de estado siguen valiendo.
            if (recibo.Agencia is null)
                recibo = await _recibos.GetConDetalleAsync(recibo.Id, ct) ?? recibo;

            var errorMail = recibo.EsConsolidadoVouchers
                ? await EnviarConsolidadoAsync(recibo, recibo.Vouchers.OrderBy(v => v.Numero).ToList(), agencia, recibo.PeriodoAnio, recibo.PeriodoMes, ct)
                : await EnviarReciboAsync(recibo, agencia, recibo.PeriodoAnio, recibo.PeriodoMes, ct);

            if (errorMail is null)
            {
                recibo.Estado = ReciboEstado.Enviado;
                recibo.FechaEnvioMail = DateTime.Now;
                recibo.UltimoErrorMail = null;
            }
            else
            {
                recibo.UltimoErrorMail = errorMail;
            }
            await _recibos.UpdateAsync(recibo, ct);
        }

        return ResultadoEmisionPorEntidad.Ok(agencia.Id, agencia.Nombre, recibo.NumeroComprobante, recibo.UltimoErrorMail);
    }

    private Recibo ConstruirRecibo(Agencia agencia, int? grupoId, decimal importe, string detalle, int anio, int mes, Configuracion config, bool esConsolidado)
    {
        var hoy = DateTime.Today;
        // No se asigna la navegación Agencia (solo el FK): el DbContext es transient, así que la agencia
        // viene de OTRO contexto y EF intentaría reinsertarla al guardar el recibo (UNIQUE Agencias.Id).
        // Para el PDF se recarga el recibo con su agencia rastreada por el contexto de _recibos.
        return new Recibo
        {
            AgenciaId = agencia.Id,
            GrupoFacturacionId = grupoId,
            PeriodoAnio = anio,
            PeriodoMes = mes,
            Importe = importe,
            Detalle = detalle,
            EsConsolidadoVouchers = esConsolidado,
            EsApoderado = config.UsarApoderado,
            NombreApoderado = config.UsarApoderado ? config.NombreApoderado : null,
            CuitApoderado = config.UsarApoderado ? config.CuitApoderado : null,
            PuntoDeVenta = config.PuntoDeVentaActivo?.Numero ?? 0,
            TipoComprobante = TipoComprobante.Recibo,
            CodigoAfip = config.CodigoAfipRecibo,
            FechaEmision = hoy,
            FechaVencimientoPago = hoy.AddDays(config.DiasVencimiento),
            Estado = ReciboEstado.Emitido
        };
    }

    /// <summary>Recibo de cuota nuevo en estado Pendiente (sin CAE), con la fecha de emisión indicada.</summary>
    private Recibo ConstruirReciboPendiente(Agencia agencia, int? grupoId, decimal importe, string detalle, DateTime fechaEmision, int anio, int mes, Configuracion config)
    {
        var recibo = ConstruirRecibo(agencia, grupoId, importe, detalle, anio, mes, config, esConsolidado: false);
        recibo.FechaEmision = fechaEmision;
        recibo.FechaVencimientoPago = fechaEmision.AddDays(config.DiasVencimiento);
        recibo.Estado = ReciboEstado.Pendiente;
        return recibo;
    }

    private Task<ServiceResult<CaeResult>> EmitirCaeAsync(Recibo recibo, Agencia agencia, Configuracion config, CancellationToken ct)
        => _afip.ObtenerCAEAsync(new ComprobanteAfipRequest
        {
            TipoComprobante = TipoComprobante.Recibo,
            CodigoAfip = recibo.CodigoAfip,
            PuntoDeVenta = recibo.PuntoDeVenta,
            CuitReceptor = PeriodoHelper.SoloDigitos(agencia.Cuit),
            ImporteTotal = recibo.Importe,
            FechaEmision = recibo.FechaEmision,
            PeriodoServicioDesde = PeriodoHelper.PrimerDia(recibo.PeriodoAnio, recibo.PeriodoMes),
            PeriodoServicioHasta = PeriodoHelper.UltimoDia(recibo.PeriodoAnio, recibo.PeriodoMes),
            FechaVencimientoPago = recibo.FechaVencimientoPago
        }, ct);

    private static void AplicarCae(Recibo recibo, CaeResult cae)
    {
        recibo.NumeroComprobante = cae.NumeroComprobante;
        recibo.CAE = cae.Cae;
        recibo.FechaVencimientoCAE = cae.FechaVencimientoCae;
    }

    private async Task<string?> EnviarReciboAsync(Recibo recibo, Agencia agencia, int anio, int mes, CancellationToken ct)
    {
        try
        {
            var pdf = await _pdf.GenerarPdfReciboAsync(recibo, ct);
            return await EnviarAsync(agencia, pdf, $"Recibo_{agencia.Nombre}_{anio}{mes:00}.pdf",
                $"Recibo {Formato.Periodo(anio, mes)} — Centro Marítimo", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falló PDF/mail para Agencia={AgenciaId}", agencia.Id);
            return ex.Message;
        }
    }

    private async Task<string?> EnviarConsolidadoAsync(Recibo recibo, IReadOnlyList<Voucher> vouchers, Agencia agencia, int anio, int mes, CancellationToken ct)
    {
        try
        {
            // PDF único: recibo (con CAE+QR) + PDFs individuales de cada voucher concatenados.
            var pdf = await _pdf.GenerarPdfDescargaAsync(vouchers, recibo, ct);
            return await EnviarAsync(agencia, pdf, $"ReciboConsolidado_{agencia.Nombre}_{anio}{mes:00}.pdf",
                $"Recibo consolidado {Formato.Periodo(anio, mes)} — Centro Marítimo", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falló PDF/mail consolidado para Agencia={AgenciaId}", agencia.Id);
            return ex.Message;
        }
    }

    private async Task<string?> EnviarAsync(Agencia agencia, byte[] pdf, string nombre, string asunto, CancellationToken ct)
    {
        var emails = agencia.Emails.Where(e => e.Activo).Select(e => e.Email).ToList();
        var envio = await _mail.EnviarReciboAsync(emails, pdf, nombre, asunto,
            "Estimados,\n\nAdjuntamos el comprobante correspondiente.\n\nSaludos.", ct);
        return envio.Success ? null : envio.ErrorMessage;
    }

    public async Task<ServiceResult<bool>> AnularReciboAsync(int reciboId, bool enviarMail, CancellationToken ct = default)
    {
        var recibo = await _recibos.GetConDetalleAsync(reciboId, ct);
        if (recibo is null) return ServiceResult<bool>.Fail("El recibo no existe.");
        if (recibo.Estado == ReciboEstado.Anulado) return ServiceResult<bool>.Fail("El recibo ya está anulado.");

        var config = await _config.GetAsync(ct);
        var hoy = DateTime.Today;

        var afipReq = new ComprobanteAfipRequest
        {
            TipoComprobante = TipoComprobante.NotaDeCredito,
            CodigoAfip = config.CodigoAfipNotaDeCredito,
            PuntoDeVenta = config.PuntoDeVentaActivo?.Numero ?? 0,
            CuitReceptor = PeriodoHelper.SoloDigitos(recibo.Agencia.Cuit),
            ImporteTotal = recibo.Importe,
            FechaEmision = hoy,
            PeriodoServicioDesde = PeriodoHelper.PrimerDia(recibo.PeriodoAnio, recibo.PeriodoMes),
            PeriodoServicioHasta = PeriodoHelper.UltimoDia(recibo.PeriodoAnio, recibo.PeriodoMes),
            FechaVencimientoPago = hoy,
            ComprobanteAsociado = new ComprobanteAsociado
            {
                Tipo = recibo.CodigoAfip,
                PuntoDeVenta = recibo.PuntoDeVenta,
                Numero = recibo.NumeroComprobante,
                CuitEmisor = PeriodoHelper.SoloDigitos(config.Cuit)
            }
        };

        var cae = await _afip.ObtenerCAEAsync(afipReq, ct);
        if (!cae.Success || cae.Data is null)
            return ServiceResult<bool>.Fail(cae.ErrorMessage ?? "AFIP no devolvió CAE para la nota de crédito.");

        var nota = new NotaDeCredito
        {
            ReciboOriginalId = recibo.Id,
            ReciboOriginal = recibo,
            PuntoDeVenta = config.PuntoDeVentaActivo?.Numero ?? 0,
            TipoComprobante = TipoComprobante.NotaDeCredito,
            CodigoAfip = config.CodigoAfipNotaDeCredito,
            NumeroComprobante = cae.Data.NumeroComprobante,
            CAE = cae.Data.Cae,
            FechaVencimientoCAE = cae.Data.FechaVencimientoCae,
            FechaEmision = hoy
        };

        recibo.Estado = ReciboEstado.Anulado;
        await _recibos.UpdateAsync(recibo, ct);
        await _notas.AddAsync(nota, ct);

        if (enviarMail)
        {
            try
            {
                var pdf = await _pdf.GenerarPdfNotaDeCreditoAsync(nota, ct);
                await EnviarAsync(recibo.Agencia, pdf, $"NotaCredito_{recibo.Id}.pdf", "Nota de crédito — Centro Marítimo", ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falló envío de NC del recibo {ReciboId}", reciboId);
            }
        }

        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> ReenviarMailAsync(int reciboId, CancellationToken ct = default)
    {
        var recibo = await _recibos.GetConDetalleAsync(reciboId, ct);
        if (recibo is null) return ServiceResult<bool>.Fail("El recibo no existe.");

        byte[] pdf = recibo.EsConsolidadoVouchers
            ? await _pdf.GenerarPdfDescargaAsync(recibo.Vouchers.ToList(), recibo, ct)
            : await _pdf.GenerarPdfReciboAsync(recibo, ct);

        var error = await EnviarAsync(recibo.Agencia, pdf,
            $"Recibo_{recibo.Agencia.Nombre}_{recibo.PeriodoAnio}{recibo.PeriodoMes:00}.pdf",
            $"Recibo {Formato.Periodo(recibo.PeriodoAnio, recibo.PeriodoMes)} — Centro Marítimo", ct);

        if (error is not null) return ServiceResult<bool>.Fail(error);

        if (recibo.Estado == ReciboEstado.Emitido)
        {
            recibo.Estado = ReciboEstado.Enviado;
            await _recibos.UpdateAsync(recibo, ct);
        }
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> MarcarPagadoAsync(int reciboId, CancellationToken ct = default)
    {
        var recibo = await _recibos.GetByIdAsync(reciboId, ct);
        if (recibo is null) return ServiceResult<bool>.Fail("El recibo no existe.");

        recibo.Estado = ReciboEstado.Pagado;
        recibo.FechaPago = DateTime.Today;
        await _recibos.UpdateAsync(recibo, ct);
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<IReadOnlyList<Recibo>>> GetPendientesAsync(FiltroPendientes filtro, CancellationToken ct = default)
        => ServiceResult<IReadOnlyList<Recibo>>.Ok(await _recibos.GetPendientesAsync(filtro, ct));
}
