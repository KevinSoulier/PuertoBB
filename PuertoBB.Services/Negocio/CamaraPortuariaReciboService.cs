using Microsoft.Extensions.Logging;
using PuertoBB.Core.Common;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Enums;
using PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models;
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

        var existentes = (await _recibos.GetPorGrupoYPeriodoAsync(grupoId, anio, mes, ct))
            .Select(r => r.EmpresaId).ToHashSet();
        var dup = grupo.Empresas
            .Where(eg => existentes.Contains(eg.EmpresaId))
            .Select(eg => eg.Empresa.Nombre)
            .ToList();
        return ServiceResult<IReadOnlyList<string>>.Ok(dup);
    }

    public async Task<ServiceResult<IReadOnlyList<EstadoEmisionEntidad<Recibo>>>> GetEstadoMasivoAsync(int grupoId, int anio, int mes, CancellationToken ct = default)
    {
        var grupo = await _grupos.GetConMiembrosAsync(grupoId, ct);
        if (grupo is null) return ServiceResult<IReadOnlyList<EstadoEmisionEntidad<Recibo>>>.Fail("El grupo no existe.");

        var recibos = (await _recibos.GetPorGrupoYPeriodoAsync(grupoId, anio, mes, ct)).ToDictionary(r => r.EmpresaId);
        var lista = grupo.Empresas
            .OrderBy(eg => eg.Empresa?.Nombre)
            .Select(eg => new EstadoEmisionEntidad<Recibo>(eg.EmpresaId, eg.Empresa?.Nombre ?? $"#{eg.EmpresaId}", recibos.GetValueOrDefault(eg.EmpresaId)))
            .ToList();
        return ServiceResult<IReadOnlyList<EstadoEmisionEntidad<Recibo>>>.Ok(lista);
    }

    public async Task<ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>> EmitirMasivoAsync(int grupoId, int anio, int mes, bool enviarMail = true, CancellationToken ct = default)
    {
        var grupo = await _grupos.GetConMiembrosAsync(grupoId, ct);
        if (grupo is null) return ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>.Fail("El grupo no existe.");

        var config = await _config.GetAsync(ct);
        var lineasGrupo = LineasDelGrupo(grupo);
        var resultados = new List<ResultadoEmisionPorEntidad>();

        foreach (var eg in grupo.Empresas)
        {
            ct.ThrowIfCancellationRequested();
            resultados.Add(await EmitirOResumirAsync(eg.Empresa, grupoId, lineasGrupo, DateTime.Today, anio, mes, config, enviarMail, ct));
        }

        return ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>.Ok(resultados);
    }

    public async Task<ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>> EnviarMasivoAsync(int grupoId, int anio, int mes, CancellationToken ct = default)
    {
        var config = await _config.GetAsync(ct);
        var resultados = new List<ResultadoEmisionPorEntidad>();

        // Una sola query para todos los recibos del grupo en el período (incluye Empresa.Emails + Lineas)
        var recibos = await _recibos.GetPorGrupoYPeriodoAsync(grupoId, anio, mes, ct);
        foreach (var recibo in recibos)
        {
            ct.ThrowIfCancellationRequested();
            // Solo se envía lo que ya tiene CAE y aún no fue enviado.
            if (string.IsNullOrEmpty(recibo.CAE) || recibo.Estado != ReciboEstado.Emitido) continue;
            resultados.Add(await ProcesarReciboAsync(recibo, recibo.Empresa, config, enviarMail: true, ct));
        }

        return ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>.Ok(resultados);
    }

    public async Task<ServiceResult<ResultadoEmisionPorEntidad>> EmitirDeGrupoAsync(int grupoId, int empresaId, int anio, int mes, bool enviarMail, CancellationToken ct = default)
    {
        var grupo = await _grupos.GetConMiembrosAsync(grupoId, ct);
        if (grupo is null) return ServiceResult<ResultadoEmisionPorEntidad>.Fail("El grupo no existe.");
        var eg = grupo.Empresas.FirstOrDefault(e => e.EmpresaId == empresaId);
        if (eg is null) return ServiceResult<ResultadoEmisionPorEntidad>.Fail("La empresa no pertenece al grupo.");

        var config = await _config.GetAsync(ct);
        var resultado = await EmitirOResumirAsync(eg.Empresa, grupoId, LineasDelGrupo(grupo), DateTime.Today, anio, mes, config, enviarMail, ct);
        return ServiceResult<ResultadoEmisionPorEntidad>.Ok(resultado);
    }

    /// <summary>Ítems del recibo según el detalle del grupo (multi-ítem). Fallback a línea única para grupos legacy sin líneas.</summary>
    private static IReadOnlyList<ReciboLineaInput> LineasDelGrupo(GrupoFacturacion grupo)
        => grupo.Lineas.Count > 0
            ? grupo.Lineas.OrderBy(l => l.Orden).Select(l => new ReciboLineaInput(l.Descripcion, l.Cantidad, l.PrecioUnitario)).ToList()
            : [new ReciboLineaInput(grupo.Nombre, 1, grupo.Importe)];

    public async Task<ServiceResult<ResultadoEmisionPorEntidad>> EmitirIndividualAsync(int empresaId, decimal importe, string detalle, DateTime fechaEmision, int anio, int mes, bool enviarMail, IReadOnlyList<ReciboLineaInput>? lineas = null, CancellationToken ct = default)
    {
        var empresa = await _empresas.GetConDetalleAsync(empresaId, ct);
        if (empresa is null) return ServiceResult<ResultadoEmisionPorEntidad>.Fail("La empresa no existe.");

        // Multi-ítem si vienen líneas; si no, un único ítem con el importe/detalle simple.
        var items = lineas is { Count: > 0 } ? lineas : [new ReciboLineaInput(detalle, 1, importe)];
        var config = await _config.GetAsync(ct);
        var resultado = await EmitirOResumirAsync(empresa, null, items, fechaEmision, anio, mes, config, enviarMail, ct);
        return ServiceResult<ResultadoEmisionPorEntidad>.Ok(resultado);
    }

    public async Task<ServiceResult<ResultadoEmisionPorEntidad>> ReintentarAsync(int reciboId, bool enviarMail, CancellationToken ct = default)
    {
        var recibo = await _recibos.GetConDetalleAsync(reciboId, ct);
        if (recibo is null) return ServiceResult<ResultadoEmisionPorEntidad>.Fail("El recibo no existe.");
        if (recibo.Estado is ReciboEstado.Pagado or ReciboEstado.Anulado)
            return ServiceResult<ResultadoEmisionPorEntidad>.Fail("El recibo no admite reintento.");

        var config = await _config.GetAsync(ct);
        var resultado = await ProcesarReciboAsync(recibo, recibo.Empresa, config, enviarMail, ct);
        return ServiceResult<ResultadoEmisionPorEntidad>.Ok(resultado);
    }

    /// <summary>
    /// Crea (en estado Pendiente, persistido antes del CAE) o resume un recibo del período, y avanza
    /// su emisión idempotentemente. Si ya está completo, devuelve "ya existe".
    /// </summary>
    private async Task<ResultadoEmisionPorEntidad> EmitirOResumirAsync(
        Empresa empresa, int? grupoId, IReadOnlyList<ReciboLineaInput> lineas, DateTime fechaEmision, int anio, int mes, Configuracion config, bool enviarMail, CancellationToken ct)
    {
        if (config.PuntoDeVentaActivo is null)
            return ResultadoEmisionPorEntidad.Fallo(empresa.Id, empresa.RazonSocial, "Configure un punto de venta activo en Configuración.");

        try
        {
            var importe = lineas.Sum(l => l.Importe);
            var detalle = string.Join(" · ", lineas.Select(l => l.Descripcion));
            var recibo = await _recibos.GetPorClaveAsync(empresa.Id, grupoId, anio, mes, ct);
            if (recibo is null)
            {
                recibo = new Recibo
                {
                    EmpresaId = empresa.Id,
                    // No se asigna la navegación Empresa (solo el FK): el DbContext es transient, así que la empresa
                    // viene de OTRO contexto y EF intentaría reinsertarla al guardar el recibo (UNIQUE Empresas.Id).
                    // Vínculo con el grupo en la entidad de relación (el recibo no conoce al grupo).
                    EmisionGrupo = grupoId is int gid
                        ? new EmisionGrupo { GrupoFacturacionId = gid, EmpresaId = empresa.Id, PeriodoAnio = anio, PeriodoMes = mes }
                        : null,
                    ReceptorNombre = empresa.Nombre,
                    ReceptorRazonSocial = empresa.RazonSocial,
                    ReceptorCuit = empresa.Cuit,
                    ReceptorDomicilio = empresa.Domicilio,
                    ReceptorCondicionIva = empresa.CondicionIva,
                    PeriodoAnio = anio,
                    PeriodoMes = mes,
                    Importe = importe,
                    Detalle = detalle,
                    Lineas = lineas.Select((l, i) => new ReciboLinea { Descripcion = l.Descripcion, Cantidad = l.Cantidad, PrecioUnitario = l.PrecioUnitario, Importe = l.Importe, Orden = i }).ToList(),
                    PuntoDeVenta = config.PuntoDeVentaActivo!.Numero,
                    TipoComprobante = TipoComprobante.Recibo,
                    CodigoAfip = config.CodigoAfipRecibo,
                    FechaEmision = fechaEmision,
                    FechaVencimientoPago = fechaEmision.AddDays(config.DiasVencimiento),
                    Estado = ReciboEstado.Pendiente
                };
                await _recibos.AddAsync(recibo, ct);
            }
            else
            {
                empresa = recibo.Empresa; // rastreada, con emails
                if (EsCompleto(recibo))
                    return ResultadoEmisionPorEntidad.Fallo(empresa.Id, empresa.Nombre, "Ya existe un recibo para este período.");

                // Recibo sin CAE (Pendiente): re-sincronizar el snapshot por si cambiaron los ítems del
                // grupo o los datos fiscales del receptor. Con CAE ya emitido queda congelado (integridad fiscal).
                if (string.IsNullOrEmpty(recibo.CAE))
                {
                    recibo.Importe = importe;
                    recibo.Detalle = detalle;
                    recibo.ReceptorNombre = empresa.Nombre;
                    recibo.ReceptorRazonSocial = empresa.RazonSocial;
                    recibo.ReceptorCuit = empresa.Cuit;
                    recibo.ReceptorDomicilio = empresa.Domicilio;
                    recibo.ReceptorCondicionIva = empresa.CondicionIva;
                    recibo.Lineas.Clear();
                    foreach (var (l, i) in lineas.Select((l, i) => (l, i)))
                        recibo.Lineas.Add(new ReciboLinea { Descripcion = l.Descripcion, Cantidad = l.Cantidad, PrecioUnitario = l.PrecioUnitario, Importe = l.Importe, Orden = i });
                }
            }

            return await ProcesarReciboAsync(recibo, empresa, config, enviarMail, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al emitir recibo para Empresa={EmpresaId}", empresa.Id);
            return ResultadoEmisionPorEntidad.Fallo(empresa.Id, empresa.Nombre, ex.Message);
        }
    }

    /// <summary>Un recibo está "completo" cuando ya no hay nada que reintentar (lógica compartida CP/CM).</summary>
    private static bool EsCompleto(Recibo r) => EstadoReciboHelper.EsCompleto(r.Estado, r.UltimoErrorMail);

    /// <summary>
    /// Avanza un recibo persistido: pide el CAE solo si falta (idempotente), y manda el mail si
    /// corresponde. Un fallo de mail NO revierte el CAE.
    /// </summary>
    private async Task<ResultadoEmisionPorEntidad> ProcesarReciboAsync(
        Recibo recibo, Empresa empresa, Configuracion config, bool enviarMail, CancellationToken ct)
    {
        // 1. CAE (idempotente: no se vuelve a pedir si ya está).
        if (string.IsNullOrEmpty(recibo.CAE))
        {
            var afipReq = new ComprobanteAfipRequest
            {
                TipoComprobante = TipoComprobante.Recibo,
                CodigoAfip = recibo.CodigoAfip,
                PuntoDeVenta = recibo.PuntoDeVenta,
                CuitReceptor = PeriodoHelper.SoloDigitos(recibo.ReceptorCuit),
                ImporteTotal = recibo.Importe,
                FechaEmision = recibo.FechaEmision,
                PeriodoServicioDesde = PeriodoHelper.PrimerDia(recibo.PeriodoAnio, recibo.PeriodoMes),
                PeriodoServicioHasta = PeriodoHelper.UltimoDia(recibo.PeriodoAnio, recibo.PeriodoMes),
                FechaVencimientoPago = recibo.FechaVencimientoPago
            };

            var cae = await _afip.ObtenerCAEAsync(afipReq, ct);
            if (!cae.Success || cae.Data is null)
            {
                recibo.Estado = ReciboEstado.Pendiente;
                recibo.UltimoErrorCae = cae.ErrorMessage ?? "AFIP no devolvió CAE.";
                await _recibos.UpdateAsync(recibo, ct);
                return ResultadoEmisionPorEntidad.Fallo(empresa.Id, empresa.Nombre, recibo.UltimoErrorCae);
            }

            recibo.NumeroComprobante = cae.Data.NumeroComprobante;
            recibo.CAE = cae.Data.Cae;
            recibo.FechaVencimientoCAE = cae.Data.FechaVencimientoCae;
            recibo.UltimoErrorCae = null;
            recibo.Estado = ReciboEstado.Emitido;
            await _recibos.UpdateAsync(recibo, ct);
        }

        // 2. Mail (opcional; un fallo NO revierte la emisión).
        if (enviarMail && recibo.Estado != ReciboEstado.Enviado)
        {
            try
            {
                var pdf = await _pdf.GenerarPdfReciboAsync(recibo, ct);
                var emails = empresa.Emails.Where(e => e.Activo).Select(e => e.Email).ToList();
                var envio = await _mail.EnviarReciboAsync(
                    emails, pdf, $"Recibo_{recibo.ReceptorNombre}_{recibo.PeriodoAnio}{recibo.PeriodoMes:00}.pdf",
                    $"Recibo {Formato.Periodo(recibo.PeriodoAnio, recibo.PeriodoMes)} — Cámara Portuaria",
                    $"Estimados,\n\nAdjuntamos el recibo correspondiente al período {Formato.Periodo(recibo.PeriodoAnio, recibo.PeriodoMes)}.\n\nSaludos.", ct);

                if (envio.Success)
                {
                    recibo.Estado = ReciboEstado.Enviado;
                    recibo.FechaEnvioMail = DateTime.Now;
                    recibo.UltimoErrorMail = null;
                }
                else
                {
                    recibo.UltimoErrorMail = envio.ErrorMessage;
                }
                await _recibos.UpdateAsync(recibo, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falló PDF/mail para Empresa={EmpresaId} (recibo queda Emitido)", empresa.Id);
                recibo.UltimoErrorMail = ex.Message;
                await _recibos.UpdateAsync(recibo, ct);
            }
        }

        return ResultadoEmisionPorEntidad.Ok(empresa.Id, empresa.Nombre, recibo.NumeroComprobante, recibo.UltimoErrorMail);
    }

    public async Task<ServiceResult<bool>> AnularReciboAsync(int reciboId, bool enviarMail, CancellationToken ct = default)
    {
        var recibo = await _recibos.GetConDetalleAsync(reciboId, ct);
        if (recibo is null) return ServiceResult<bool>.Fail("El recibo no existe.");
        if (recibo.Estado == ReciboEstado.Anulado) return ServiceResult<bool>.Fail("El recibo ya está anulado.");
        if (string.IsNullOrEmpty(recibo.CAE))
            return ServiceResult<bool>.Fail("El recibo no tiene CAE: emítalo antes de anularlo.");

        var config = await _config.GetAsync(ct);
        if (config.PuntoDeVentaActivo is null)
            return ServiceResult<bool>.Fail("Configure un punto de venta activo en Configuración.");
        var hoy = DateTime.Today;

        var afipReq = new ComprobanteAfipRequest
        {
            TipoComprobante = TipoComprobante.NotaDeCredito,
            CodigoAfip = config.CodigoAfipNotaDeCredito,
            PuntoDeVenta = config.PuntoDeVentaActivo!.Numero,
            CuitReceptor = PeriodoHelper.SoloDigitos(recibo.ReceptorCuit),
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
            PuntoDeVenta = config.PuntoDeVentaActivo!.Numero,
            TipoComprobante = TipoComprobante.NotaDeCredito,
            CodigoAfip = config.CodigoAfipNotaDeCredito,
            NumeroComprobante = cae.Data.NumeroComprobante,
            CAE = cae.Data.Cae,
            FechaVencimientoCAE = cae.Data.FechaVencimientoCae,
            FechaEmision = hoy
        };

        // Anular recibo + persistir NC en un único SaveChanges (atómico: evita NC sin registro si falla).
        await _recibos.AnularConNotaAsync(recibo, nota, ct);

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
        if (string.IsNullOrEmpty(recibo.CAE)) return ServiceResult<bool>.Fail("El recibo no tiene CAE: emítalo antes de reenviar.");

        var pdf = await _pdf.GenerarPdfReciboAsync(recibo, ct);
        var emails = recibo.Empresa.Emails.Where(e => e.Activo).Select(e => e.Email).ToList();
        var envio = await _mail.EnviarReciboAsync(emails, pdf,
            $"Recibo_{recibo.ReceptorNombre}_{recibo.PeriodoAnio}{recibo.PeriodoMes:00}.pdf",
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
        if (recibo.Estado == ReciboEstado.Anulado) return ServiceResult<bool>.Fail("No se puede marcar como pagado un recibo anulado.");
        if (recibo.Estado == ReciboEstado.Pendiente) return ServiceResult<bool>.Fail("El recibo aún no tiene CAE: no se puede marcar como pagado.");

        recibo.Estado = ReciboEstado.Pagado;
        recibo.FechaPago = DateTime.Today;
        await _recibos.UpdateAsync(recibo, ct);
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<IReadOnlyList<Recibo>>> GetPendientesAsync(FiltroPendientes filtro, CancellationToken ct = default)
        => ServiceResult<IReadOnlyList<Recibo>>.Ok(await _recibos.GetPendientesAsync(filtro, ct));
}
