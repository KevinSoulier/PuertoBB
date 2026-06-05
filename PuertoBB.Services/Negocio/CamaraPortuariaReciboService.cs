using Microsoft.Extensions.Logging;
using PuertoBB.Core.Common;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Enums;
using PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Afip;
using PuertoBB.Core.Models.Resultados;
using PuertoBB.Services.Common;

namespace PuertoBB.Services.Negocio;

public class CamaraPortuariaReciboService : ICamaraPortuariaReciboService
{
    private readonly IReciboRepository _recibos;
    private readonly IGrupoFacturacionRepository _grupos;
    private readonly IEmpresaRepository _empresas;
    private readonly INotaDeCreditoRepository _notas;
    private readonly IConfiguracionRepository _config;
    private readonly IAfipService _afip;
    private readonly ICamaraPortuariaPdfService _pdf;
    private readonly IMailService _mail;
    private readonly ILogger<CamaraPortuariaReciboService> _logger;

    public CamaraPortuariaReciboService(
        IReciboRepository recibos,
        IGrupoFacturacionRepository grupos,
        IEmpresaRepository empresas,
        INotaDeCreditoRepository notas,
        IConfiguracionRepository config,
        IAfipService afip,
        ICamaraPortuariaPdfService pdf,
        IMailService mail,
        ILogger<CamaraPortuariaReciboService> logger)
    {
        _recibos = recibos;
        _grupos = grupos;
        _empresas = empresas;
        _notas = notas;
        _config = config;
        _afip = afip;
        _pdf = pdf;
        _mail = mail;
        _logger = logger;
    }

    public async Task<ServiceResult<IReadOnlyList<string>>> GetDuplicadosAsync(int grupoId, int anio, int mes, CancellationToken ct = default)
    {
        var grupo = await _grupos.GetConMiembrosAsync(grupoId, ct);
        if (grupo is null) return ServiceResult<IReadOnlyList<string>>.Fail("El grupo no existe.");

        var dup = new List<string>();
        foreach (var eg in grupo.Empresas)
            if (await _recibos.ExisteAsync(eg.EmpresaId, grupoId, anio, mes, ct))
                dup.Add(eg.Empresa.Nombre);

        return ServiceResult<IReadOnlyList<string>>.Ok(dup);
    }

    public async Task<ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>> EmitirMasivoAsync(int grupoId, int anio, int mes, CancellationToken ct = default)
    {
        var grupo = await _grupos.GetConMiembrosAsync(grupoId, ct);
        if (grupo is null) return ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>.Fail("El grupo no existe.");

        var config = await _config.GetAsync(ct);
        var resultados = new List<ResultadoEmisionPorEntidad>();

        foreach (var eg in grupo.Empresas)
        {
            ct.ThrowIfCancellationRequested();
            var empresa = eg.Empresa;
            resultados.Add(await EmitirParaEmpresaAsync(empresa, grupoId, grupo.Importe, grupo.Nombre, anio, mes, config, ct));
        }

        return ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>.Ok(resultados);
    }

    public async Task<ServiceResult<ResultadoEmisionPorEntidad>> EmitirIndividualAsync(int empresaId, decimal importe, string detalle, int anio, int mes, CancellationToken ct = default)
    {
        var empresa = await _empresas.GetConDetalleAsync(empresaId, ct);
        if (empresa is null) return ServiceResult<ResultadoEmisionPorEntidad>.Fail("La empresa no existe.");

        var config = await _config.GetAsync(ct);
        var resultado = await EmitirParaEmpresaAsync(empresa, null, importe, detalle, anio, mes, config, ct);
        return ServiceResult<ResultadoEmisionPorEntidad>.Ok(resultado);
    }

    private async Task<ResultadoEmisionPorEntidad> EmitirParaEmpresaAsync(
        Empresa empresa, int? grupoId, decimal importe, string detalle, int anio, int mes, Configuracion config, CancellationToken ct)
    {
        try
        {
            if (await _recibos.ExisteAsync(empresa.Id, grupoId, anio, mes, ct))
                return ResultadoEmisionPorEntidad.Fallo(empresa.Id, empresa.Nombre, "Ya existe un recibo para este período.");

            var hoy = DateTime.Today;
            var recibo = new Recibo
            {
                EmpresaId = empresa.Id,
                Empresa = empresa,
                GrupoFacturacionId = grupoId,
                PeriodoAnio = anio,
                PeriodoMes = mes,
                Importe = importe,
                Detalle = detalle,
                PuntoDeVenta = config.PuntoDeVenta,
                TipoComprobante = TipoComprobante.Recibo,
                CodigoAfip = config.CodigoAfipRecibo,
                FechaEmision = hoy,
                FechaVencimientoPago = hoy.AddDays(config.DiasVencimiento),
                Estado = ReciboEstado.Emitido
            };

            var afipReq = new ComprobanteAfipRequest
            {
                TipoComprobante = TipoComprobante.Recibo,
                CodigoAfip = config.CodigoAfipRecibo,
                PuntoDeVenta = config.PuntoDeVenta,
                CuitReceptor = PeriodoHelper.SoloDigitos(empresa.Cuit),
                ImporteTotal = importe,
                FechaEmision = hoy,
                PeriodoServicioDesde = PeriodoHelper.PrimerDia(anio, mes),
                PeriodoServicioHasta = PeriodoHelper.UltimoDia(anio, mes),
                FechaVencimientoPago = recibo.FechaVencimientoPago
            };

            var cae = await _afip.ObtenerCAEAsync(afipReq, ct);
            if (!cae.Success || cae.Data is null)
                return ResultadoEmisionPorEntidad.Fallo(empresa.Id, empresa.Nombre, cae.ErrorMessage ?? "AFIP no devolvió CAE.");

            recibo.NumeroComprobante = cae.Data.NumeroComprobante;
            recibo.CAE = cae.Data.Cae;
            recibo.FechaVencimientoCAE = cae.Data.FechaVencimientoCae;

            await _recibos.AddAsync(recibo, ct);

            // PDF + mail (un fallo de mail NO revierte la emisión).
            string? errorMail = null;
            try
            {
                var pdf = await _pdf.GenerarPdfReciboAsync(recibo, ct);
                var emails = empresa.Emails.Where(e => e.Activo).Select(e => e.Email).ToList();
                var envio = await _mail.EnviarReciboAsync(
                    emails, pdf, $"Recibo_{empresa.Nombre}_{anio}{mes:00}.pdf",
                    $"Recibo {Formato.Periodo(anio, mes)} — Cámara Portuaria",
                    $"Estimados,\n\nAdjuntamos el recibo correspondiente al período {Formato.Periodo(anio, mes)}.\n\nSaludos.", ct);

                if (envio.Success)
                {
                    recibo.Estado = ReciboEstado.Enviado;
                    await _recibos.UpdateAsync(recibo, ct);
                }
                else
                {
                    errorMail = envio.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falló PDF/mail para Empresa={EmpresaId} (recibo queda Emitido)", empresa.Id);
                errorMail = ex.Message;
            }

            return ResultadoEmisionPorEntidad.Ok(empresa.Id, empresa.Nombre, recibo.NumeroComprobante, errorMail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al emitir recibo para Empresa={EmpresaId}", empresa.Id);
            return ResultadoEmisionPorEntidad.Fallo(empresa.Id, empresa.Nombre, ex.Message);
        }
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
            CuitReceptor = PeriodoHelper.SoloDigitos(recibo.Empresa.Cuit),
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
                var emails = recibo.Empresa.Emails.Where(e => e.Activo).Select(e => e.Email).ToList();
                await _mail.EnviarReciboAsync(emails, pdf, $"NotaCredito_{recibo.Id}.pdf",
                    "Nota de crédito — Cámara Portuaria",
                    "Estimados,\n\nAdjuntamos la nota de crédito correspondiente.\n\nSaludos.", ct);
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

        var pdf = await _pdf.GenerarPdfReciboAsync(recibo, ct);
        var emails = recibo.Empresa.Emails.Where(e => e.Activo).Select(e => e.Email).ToList();
        var envio = await _mail.EnviarReciboAsync(emails, pdf,
            $"Recibo_{recibo.Empresa.Nombre}_{recibo.PeriodoAnio}{recibo.PeriodoMes:00}.pdf",
            $"Recibo {Formato.Periodo(recibo.PeriodoAnio, recibo.PeriodoMes)} — Cámara Portuaria",
            "Estimados,\n\nReenviamos el recibo correspondiente.\n\nSaludos.", ct);

        if (!envio.Success) return ServiceResult<bool>.Fail(envio.ErrorMessage ?? "No se pudo enviar el mail.");

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
