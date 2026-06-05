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
            var agenciaId = grupo.Key;
            var nombreAgencia = grupo.First().Agencia.Nombre;
            try
            {
                if (await _recibos.ExisteConsolidadoAsync(agenciaId, anio, mes, ct))
                {
                    resultados.Add(ResultadoCierrePorAgencia.Omitida(agenciaId, nombreAgencia, "Ya existe un recibo consolidado para este período."));
                    continue;
                }

                // Cargar la agencia rastreada (con emails) para evitar reinsertar entidades detached al persistir.
                var agencia = await _agencias.GetConDetalleAsync(agenciaId, ct);
                if (agencia is null)
                {
                    resultados.Add(ResultadoCierrePorAgencia.Fallo(agenciaId, nombreAgencia, "La agencia no existe."));
                    continue;
                }

                var vouchersAgencia = grupo.OrderBy(v => v.Numero).ToList();
                var importe = vouchersAgencia.Sum(v => v.Importe);
                var detalle = "Vouchers Nros: " + string.Join(", ", vouchersAgencia.Select(v => v.Numero));

                var recibo = ConstruirRecibo(agencia, null, importe, detalle, anio, mes, config, esConsolidado: true);

                var cae = await EmitirCaeAsync(recibo, agencia, config, ct);
                if (!cae.Success || cae.Data is null)
                {
                    resultados.Add(ResultadoCierrePorAgencia.Fallo(agencia.Id, agencia.Nombre, cae.ErrorMessage ?? "AFIP no devolvió CAE."));
                    continue;
                }
                AplicarCae(recibo, cae.Data);

                await _recibos.AddAsync(recibo, ct);
                await _vouchers.MarcarConsolidadosAsync(vouchersAgencia.Select(v => v.Id), recibo.Id, ct);

                var errorMail = await EnviarConsolidadoAsync(recibo, vouchersAgencia, agencia, anio, mes, ct);
                if (errorMail is null)
                {
                    recibo.Estado = ReciboEstado.Enviado;
                    await _recibos.UpdateAsync(recibo, ct);
                }

                resultados.Add(ResultadoCierrePorAgencia.Ok(agencia.Id, agencia.Nombre, vouchersAgencia.Count, importe, recibo.NumeroComprobante, errorMail));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cerrar período para Agencia={AgenciaId}", agenciaId);
                resultados.Add(ResultadoCierrePorAgencia.Fallo(agenciaId, nombreAgencia, ex.Message));
            }
        }

        return ServiceResult<IReadOnlyList<ResultadoCierrePorAgencia>>.Ok(resultados);
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
            resultados.Add(await EmitirCuotaAsync(ag.Agencia, grupoId, grupo.Importe, grupo.Nombre, anio, mes, config, ct));
        }

        return ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>.Ok(resultados);
    }

    public async Task<ServiceResult<ResultadoEmisionPorEntidad>> EmitirIndividualAsync(int agenciaId, decimal importe, string detalle, int anio, int mes, CancellationToken ct = default)
    {
        var agencia = await _agencias.GetConDetalleAsync(agenciaId, ct);
        if (agencia is null) return ServiceResult<ResultadoEmisionPorEntidad>.Fail("La agencia no existe.");

        var config = await _config.GetAsync(ct);
        return ServiceResult<ResultadoEmisionPorEntidad>.Ok(await EmitirCuotaAsync(agencia, null, importe, detalle, anio, mes, config, ct));
    }

    private async Task<ResultadoEmisionPorEntidad> EmitirCuotaAsync(
        Agencia agencia, int? grupoId, decimal importe, string detalle, int anio, int mes, Configuracion config, CancellationToken ct)
    {
        try
        {
            if (await _recibos.ExisteAsync(agencia.Id, grupoId, anio, mes, ct))
                return ResultadoEmisionPorEntidad.Fallo(agencia.Id, agencia.Nombre, "Ya existe un recibo para este período.");

            var recibo = ConstruirRecibo(agencia, grupoId, importe, detalle, anio, mes, config, esConsolidado: false);

            var cae = await EmitirCaeAsync(recibo, agencia, config, ct);
            if (!cae.Success || cae.Data is null)
                return ResultadoEmisionPorEntidad.Fallo(agencia.Id, agencia.Nombre, cae.ErrorMessage ?? "AFIP no devolvió CAE.");
            AplicarCae(recibo, cae.Data);

            await _recibos.AddAsync(recibo, ct);

            string? errorMail = await EnviarReciboAsync(recibo, agencia, anio, mes, ct);
            if (errorMail is null)
            {
                recibo.Estado = ReciboEstado.Enviado;
                await _recibos.UpdateAsync(recibo, ct);
            }

            return ResultadoEmisionPorEntidad.Ok(agencia.Id, agencia.Nombre, recibo.NumeroComprobante, errorMail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al emitir cuota para Agencia={AgenciaId}", agencia.Id);
            return ResultadoEmisionPorEntidad.Fallo(agencia.Id, agencia.Nombre, ex.Message);
        }
    }

    private Recibo ConstruirRecibo(Agencia agencia, int? grupoId, decimal importe, string detalle, int anio, int mes, Configuracion config, bool esConsolidado)
    {
        var hoy = DateTime.Today;
        return new Recibo
        {
            AgenciaId = agencia.Id,
            Agencia = agencia,
            GrupoFacturacionId = grupoId,
            PeriodoAnio = anio,
            PeriodoMes = mes,
            Importe = importe,
            Detalle = detalle,
            EsConsolidadoVouchers = esConsolidado,
            EsApoderado = config.UsarApoderado,
            NombreApoderado = config.UsarApoderado ? config.NombreApoderado : null,
            CuitApoderado = config.UsarApoderado ? config.CuitApoderado : null,
            PuntoDeVenta = config.PuntoDeVenta,
            TipoComprobante = TipoComprobante.Recibo,
            CodigoAfip = config.CodigoAfipRecibo,
            FechaEmision = hoy,
            FechaVencimientoPago = hoy.AddDays(config.DiasVencimiento),
            Estado = ReciboEstado.Emitido
        };
    }

    private Task<ServiceResult<CaeResult>> EmitirCaeAsync(Recibo recibo, Agencia agencia, Configuracion config, CancellationToken ct)
        => _afip.ObtenerCAEAsync(new ComprobanteAfipRequest
        {
            TipoComprobante = TipoComprobante.Recibo,
            CodigoAfip = config.CodigoAfipRecibo,
            PuntoDeVenta = config.PuntoDeVenta,
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
            var pdf = await _pdf.GenerarPdfConsolidadoAsync(recibo, vouchers, ct);
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
            PuntoDeVenta = config.PuntoDeVenta,
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
            PuntoDeVenta = config.PuntoDeVenta,
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
            ? await _pdf.GenerarPdfConsolidadoAsync(recibo, recibo.Vouchers, ct)
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
