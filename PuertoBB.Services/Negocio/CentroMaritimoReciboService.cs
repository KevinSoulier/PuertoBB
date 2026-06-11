using Microsoft.Extensions.Logging;
using PuertoBB.Core.Common;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Enums;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models;
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

    public Task<ServiceResult<IReadOnlyList<ResultadoCierrePorAgencia>>> CerrarPeriodoAsync(int anio, int mes, CancellationToken ct = default)
        => ProcesarPeriodoAsync(anio, mes, enviarMail: true, ct);

    public Task<ServiceResult<IReadOnlyList<ResultadoCierrePorAgencia>>> EmitirRecibosPeriodoAsync(int anio, int mes, CancellationToken ct = default)
        => ProcesarPeriodoAsync(anio, mes, enviarMail: false, ct);

    private async Task<ServiceResult<IReadOnlyList<ResultadoCierrePorAgencia>>> ProcesarPeriodoAsync(int anio, int mes, bool enviarMail, CancellationToken ct)
    {
        var config = await _config.GetAsync(ct);
        if (config.PuntoDeVentaActivo is null)
            return ServiceResult<IReadOnlyList<ResultadoCierrePorAgencia>>.Fail("Configure un punto de venta activo en Configuración.");

        var porAgencia = await GetAgenciasAProcesarAsync(anio, mes, ct);
        var resultados = new List<ResultadoCierrePorAgencia>();
        foreach (var (agId, vouchersAgencia) in porAgencia)
        {
            ct.ThrowIfCancellationRequested();
            resultados.Add(await ProcesarCierreAgenciaAsync(agId, vouchersAgencia, anio, mes, config, enviarMail, ct));
        }

        return ServiceResult<IReadOnlyList<ResultadoCierrePorAgencia>>.Ok(resultados);
    }

    /// <summary>
    /// Agencias a procesar en el período: los vouchers libres agrupados por agencia, más las agencias
    /// con consolidado Pendiente (sin CAE) aunque ya no tengan vouchers libres → reintento.
    /// </summary>
    private async Task<Dictionary<int, IReadOnlyList<Voucher>>> GetAgenciasAProcesarAsync(int anio, int mes, CancellationToken ct)
    {
        var pendientes = await _vouchers.GetPendientesByPeriodoAsync(anio, mes, ct);
        var porAgencia = pendientes.GroupBy(v => v.AgenciaId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Voucher>)g.OrderBy(v => v.Numero).ToList());

        var conPendiente = await _recibos.GetAgenciasConConsolidadoPendienteAsync(anio, mes, ct);
        foreach (var agId in conPendiente.Where(id => !porAgencia.ContainsKey(id)))
            porAgencia[agId] = [];

        return porAgencia;
    }

    public Task<ServiceResult<ResultadoCierrePorAgencia>> CerrarPeriodoAgenciaAsync(int agenciaId, int anio, int mes, CancellationToken ct = default)
        => ProcesarPeriodoAgenciaAsync(agenciaId, anio, mes, enviarMail: true, ct);

    public Task<ServiceResult<ResultadoCierrePorAgencia>> EmitirReciboAgenciaAsync(int agenciaId, int anio, int mes, CancellationToken ct = default)
        => ProcesarPeriodoAgenciaAsync(agenciaId, anio, mes, enviarMail: false, ct);

    /// <summary>
    /// Consolida los vouchers libres de una agencia en un recibo (valida duplicado, obtiene CAE,
    /// persiste, marca vouchers y opcionalmente envía el PDF único por mail). Si no hay vouchers
    /// libres pero quedó un consolidado Pendiente (sin CAE), lo reintenta.
    /// </summary>
    private async Task<ServiceResult<ResultadoCierrePorAgencia>> ProcesarPeriodoAgenciaAsync(int agenciaId, int anio, int mes, bool enviarMail, CancellationToken ct)
    {
        var config = await _config.GetAsync(ct);
        if (config.PuntoDeVentaActivo is null)
            return ServiceResult<ResultadoCierrePorAgencia>.Fail("Configure un punto de venta activo en Configuración.");

        var pendientes = await _vouchers.GetPendientesByPeriodoAsync(anio, mes, ct);
        var vouchersAgencia = pendientes.Where(v => v.AgenciaId == agenciaId).OrderBy(v => v.Numero).ToList();
        if (vouchersAgencia.Count == 0)
        {
            // Sin vouchers libres solo se sigue si hay un consolidado Pendiente (sin CAE) para reintentar.
            var consolidado = await _recibos.GetConsolidadoAsync(agenciaId, anio, mes, ct);
            if (consolidado is null || !string.IsNullOrEmpty(consolidado.CAE))
                return ServiceResult<ResultadoCierrePorAgencia>.Fail("La agencia no tiene vouchers pendientes en el período.");
        }

        var resultado = await ProcesarCierreAgenciaAsync(agenciaId, vouchersAgencia, anio, mes, config, enviarMail, ct);
        return resultado.Exito
            ? ServiceResult<ResultadoCierrePorAgencia>.Ok(resultado)
            : ServiceResult<ResultadoCierrePorAgencia>.Fail(resultado.ErrorEmision ?? "No se pudo procesar el recibo de la agencia.");
    }

    private async Task<ResultadoCierrePorAgencia> ProcesarCierreAgenciaAsync(
        int agenciaId, IReadOnlyList<Voucher> vouchersAgencia, int anio, int mes, Configuracion config, bool enviarMail, CancellationToken ct)
    {
        var nombreAgencia = vouchersAgencia.FirstOrDefault()?.Agencia?.Nombre ?? $"#{agenciaId}";
        try
        {
            var existente = await _recibos.GetConsolidadoAsync(agenciaId, anio, mes, ct);
            if (existente is not null)
            {
                var agenciaExistente = existente.Agencia;
                if (EsCompleto(existente))
                    return ResultadoCierrePorAgencia.Omitida(agenciaId, agenciaExistente?.Nombre ?? nombreAgencia, "Ya existe un recibo consolidado para este período.");

                if (string.IsNullOrEmpty(existente.CAE))
                {
                    // Recibo Pendiente: vincular nuevos vouchers (si los hay) y re-sincronizar snapshot.
                    if (vouchersAgencia.Count > 0)
                        await _vouchers.MarcarConsolidadosAsync(vouchersAgencia.Select(v => v.Id), existente.Id, ct);

                    var todosVouchers = existente.Vouchers.Concat(vouchersAgencia).OrderBy(v => v.Numero).ToList();
                    existente.Importe = todosVouchers.Sum(v => v.Importe);
                    existente.Detalle = "Vouchers Nros: " + string.Join(", ", todosVouchers.Select(v => v.Numero));
                    existente.Lineas.Clear();
                    foreach (var (v, i) in todosVouchers.Select((v, i) => (v, i)))
                        existente.Lineas.Add(new ReciboLinea
                        {
                            Descripcion    = $"Voucher {v.Numero} — {v.Barco?.Nombre ?? $"#{v.BarcoId}"} — {Formato.Fecha(v.Fecha)}",
                            Cantidad       = 1,
                            PrecioUnitario = v.Importe,
                            Importe        = v.Importe,
                            Orden          = i
                        });
                }

                var resExistente = await ProcesarReciboAsync(existente, existente.Agencia, config, enviarMail, ct);
                return resExistente.Exito
                    ? ResultadoCierrePorAgencia.Ok(agenciaId, agenciaExistente?.Nombre ?? nombreAgencia, existente.Vouchers.Count, existente.Importe, existente.NumeroComprobante, resExistente.ErrorMail)
                    : ResultadoCierrePorAgencia.Fallo(agenciaId, agenciaExistente?.Nombre ?? nombreAgencia, resExistente.ErrorEmision ?? "No se pudo procesar el recibo consolidado.");
            }

            // Caso nuevo: persistir Pendiente ANTES de pedir el CAE.
            var agencia = await _agencias.GetConDetalleAsync(agenciaId, ct);
            if (agencia is null)
                return ResultadoCierrePorAgencia.Fallo(agenciaId, nombreAgencia, "La agencia no existe.");

            var importe = vouchersAgencia.Sum(v => v.Importe);
            var detalle = "Vouchers Nros: " + string.Join(", ", vouchersAgencia.Select(v => v.Numero));

            var recibo = ConstruirRecibo(agencia, null, importe, detalle, anio, mes, config, esConsolidado: true);
            recibo.Estado = ReciboEstado.Pendiente;

            // Snapshot del detalle: una línea por voucher.
            recibo.Lineas = vouchersAgencia
                .Select((v, i) => new ReciboLinea
                {
                    Descripcion    = $"Voucher {v.Numero} — {v.Barco?.Nombre ?? $"#{v.BarcoId}"} — {Formato.Fecha(v.Fecha)}",
                    Cantidad       = 1,
                    PrecioUnitario = v.Importe,
                    Importe        = v.Importe,
                    Orden          = i
                })
                .ToList();

            // Persistir recibo + vincular vouchers en un único SaveChanges (atómico).
            await _recibos.AddConVouchersAsync(recibo, vouchersAgencia.Select(v => v.Id).ToList(), ct);

            var resultado = await ProcesarReciboAsync(recibo, agencia, config, enviarMail, ct);
            return resultado.Exito
                ? ResultadoCierrePorAgencia.Ok(agencia.Id, agencia.Nombre, vouchersAgencia.Count, importe, recibo.NumeroComprobante, resultado.ErrorMail)
                : ResultadoCierrePorAgencia.Fallo(agencia.Id, agencia.Nombre, resultado.ErrorEmision ?? "No se pudo emitir el recibo.");
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

        var existentes = (await _recibos.GetPorGrupoYPeriodoAsync(grupoId, anio, mes, ct))
            .Select(r => r.AgenciaId).ToHashSet();
        var dup = grupo.Agencias
            .Where(ag => existentes.Contains(ag.AgenciaId))
            .Select(ag => ag.Agencia.Nombre)
            .ToList();
        return ServiceResult<IReadOnlyList<string>>.Ok(dup);
    }

    public async Task<ServiceResult<IReadOnlyList<EstadoEmisionEntidad<Recibo>>>> GetEstadoMasivoAsync(int grupoId, int anio, int mes, CancellationToken ct = default)
    {
        var grupo = await _grupos.GetConMiembrosAsync(grupoId, ct);
        if (grupo is null) return ServiceResult<IReadOnlyList<EstadoEmisionEntidad<Recibo>>>.Fail("El grupo no existe.");

        var recibos = (await _recibos.GetPorGrupoYPeriodoAsync(grupoId, anio, mes, ct)).ToDictionary(r => r.AgenciaId);
        var lista = grupo.Agencias
            .OrderBy(ag => ag.Agencia?.Nombre)
            .Select(ag => new EstadoEmisionEntidad<Recibo>(ag.AgenciaId, ag.Agencia?.Nombre ?? $"#{ag.AgenciaId}", recibos.GetValueOrDefault(ag.AgenciaId)))
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

        foreach (var ag in grupo.Agencias)
        {
            ct.ThrowIfCancellationRequested();
            resultados.Add(await EmitirOResumirAsync(ag.Agencia, grupoId, lineasGrupo, DateTime.Today, anio, mes, config, enviarMail, ct));
        }

        return ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>.Ok(resultados);
    }

    public async Task<ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>> EnviarMasivoAsync(int grupoId, int anio, int mes, CancellationToken ct = default)
    {
        var config = await _config.GetAsync(ct);
        var resultados = new List<ResultadoEmisionPorEntidad>();

        // Una sola query para todos los recibos del grupo en el período (incluye Agencia.Emails + Lineas)
        var recibos = await _recibos.GetPorGrupoYPeriodoAsync(grupoId, anio, mes, ct);
        foreach (var recibo in recibos)
        {
            ct.ThrowIfCancellationRequested();
            // Solo se envía lo que ya tiene CAE y aún no fue enviado.
            if (string.IsNullOrEmpty(recibo.CAE) || recibo.Estado != ReciboEstado.Emitido) continue;
            resultados.Add(await ProcesarReciboAsync(recibo, recibo.Agencia, config, enviarMail: true, ct));
        }

        return ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>.Ok(resultados);
    }

    public async Task<ServiceResult<ResultadoEmisionPorEntidad>> EmitirDeGrupoAsync(int grupoId, int agenciaId, int anio, int mes, bool enviarMail, CancellationToken ct = default)
    {
        var grupo = await _grupos.GetConMiembrosAsync(grupoId, ct);
        if (grupo is null) return ServiceResult<ResultadoEmisionPorEntidad>.Fail("El grupo no existe.");
        var ag = grupo.Agencias.FirstOrDefault(a => a.AgenciaId == agenciaId);
        if (ag is null) return ServiceResult<ResultadoEmisionPorEntidad>.Fail("La agencia no pertenece al grupo.");

        var config = await _config.GetAsync(ct);
        var resultado = await EmitirOResumirAsync(ag.Agencia, grupoId, LineasDelGrupo(grupo), DateTime.Today, anio, mes, config, enviarMail, ct);
        return ServiceResult<ResultadoEmisionPorEntidad>.Ok(resultado);
    }

    /// <summary>Ítems del recibo según el detalle del grupo (multi-ítem). Fallback a línea única para grupos legacy sin líneas.</summary>
    private static IReadOnlyList<ReciboLineaInput> LineasDelGrupo(GrupoFacturacion grupo)
        => grupo.Lineas.Count > 0
            ? grupo.Lineas.OrderBy(l => l.Orden).Select(l => new ReciboLineaInput(l.Descripcion, l.Cantidad, l.PrecioUnitario)).ToList()
            : [new ReciboLineaInput(grupo.Nombre, 1, grupo.Importe)];

    public async Task<ServiceResult<ResultadoEmisionPorEntidad>> EmitirIndividualAsync(int agenciaId, decimal importe, string detalle, DateTime fechaEmision, int anio, int mes, bool enviarMail, IReadOnlyList<ReciboLineaInput>? lineas = null, CancellationToken ct = default)
    {
        var agencia = await _agencias.GetConDetalleAsync(agenciaId, ct);
        if (agencia is null) return ServiceResult<ResultadoEmisionPorEntidad>.Fail("La agencia no existe.");

        // Multi-ítem si vienen líneas; si no, un único ítem con el importe/detalle simple.
        var items = lineas is { Count: > 0 } ? lineas : [new ReciboLineaInput(detalle, 1, importe)];
        var config = await _config.GetAsync(ct);
        return ServiceResult<ResultadoEmisionPorEntidad>.Ok(await EmitirOResumirAsync(agencia, null, items, fechaEmision, anio, mes, config, enviarMail, ct));
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
        Agencia agencia, int? grupoId, IReadOnlyList<ReciboLineaInput> lineas, DateTime fechaEmision, int anio, int mes, Configuracion config, bool enviarMail, CancellationToken ct)
    {
        if (config.PuntoDeVentaActivo is null)
            return ResultadoEmisionPorEntidad.Fallo(agencia.Id, agencia.Nombre, "Configure un punto de venta activo en Configuración.");

        try
        {
            var importe = lineas.Sum(l => l.Importe);
            var detalle = string.Join(" · ", lineas.Select(l => l.Descripcion));
            var recibo = await _recibos.GetPorClaveAsync(agencia.Id, grupoId, anio, mes, ct);
            if (recibo is null)
            {
                recibo = ConstruirReciboPendiente(agencia, grupoId, importe, detalle, fechaEmision, anio, mes, config);
                // Snapshot multi-ítem (reemplaza la línea única por defecto que arma ConstruirRecibo).
                recibo.Lineas = lineas.Select((l, i) => new ReciboLinea { Descripcion = l.Descripcion, Cantidad = l.Cantidad, PrecioUnitario = l.PrecioUnitario, Importe = l.Importe, Orden = i }).ToList();
                await _recibos.AddAsync(recibo, ct);
            }
            else
            {
                agencia = recibo.Agencia; // rastreada, con emails
                if (EsCompleto(recibo))
                    return ResultadoEmisionPorEntidad.Fallo(agencia.Id, agencia.Nombre, "Ya existe un recibo para este período.");

                // Recibo sin CAE (Pendiente): re-sincronizar el snapshot por si cambiaron los ítems del
                // grupo o los datos fiscales del receptor. Con CAE ya emitido queda congelado (integridad fiscal).
                if (string.IsNullOrEmpty(recibo.CAE))
                {
                    recibo.Importe = importe;
                    recibo.Detalle = detalle;
                    recibo.ReceptorNombre = agencia.Nombre;
                    recibo.ReceptorRazonSocial = agencia.RazonSocial;
                    recibo.ReceptorCuit = agencia.Cuit;
                    recibo.ReceptorDomicilio = agencia.Domicilio;
                    recibo.ReceptorCondicionIva = agencia.CondicionIva;
                    recibo.Lineas.Clear();
                    foreach (var (l, i) in lineas.Select((l, i) => (l, i)))
                        recibo.Lineas.Add(new ReciboLinea { Descripcion = l.Descripcion, Cantidad = l.Cantidad, PrecioUnitario = l.PrecioUnitario, Importe = l.Importe, Orden = i });
                }
            }

            return await ProcesarReciboAsync(recibo, agencia, config, enviarMail, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al emitir cuota para Agencia={AgenciaId}", agencia.Id);
            return ResultadoEmisionPorEntidad.Fallo(agencia.Id, agencia.Nombre, ex.Message);
        }
    }

    /// <summary>Un recibo está "completo" cuando ya no hay nada que reintentar (lógica compartida CP/CM).</summary>
    private static bool EsCompleto(Recibo r) => EstadoReciboHelper.EsCompleto(r.Estado, r.UltimoErrorMail);

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
            var cae = await EmitirCaeAsync(recibo, config, ct);
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
        // Para el mail se recarga el recibo con su agencia rastreada por el contexto de _recibos.
        return new Recibo
        {
            AgenciaId = agencia.Id,
            // Vínculo con el grupo en la entidad de relación (el recibo no conoce al grupo).
            EmisionGrupo = grupoId is int gid
                ? new EmisionGrupo { GrupoFacturacionId = gid, AgenciaId = agencia.Id, PeriodoAnio = anio, PeriodoMes = mes }
                : null,
            ReceptorNombre = agencia.Nombre,
            ReceptorRazonSocial = agencia.RazonSocial,
            ReceptorCuit = agencia.Cuit,
            ReceptorDomicilio = agencia.Domicilio,
            ReceptorCondicionIva = agencia.CondicionIva,
            PeriodoAnio = anio,
            PeriodoMes = mes,
            Importe = importe,
            Detalle = detalle,
            Lineas = [new ReciboLinea { Descripcion = detalle, Cantidad = 1, PrecioUnitario = importe, Importe = importe, Orden = 0 }],
            EsConsolidadoVouchers = esConsolidado,
            EsApoderado = config.UsarApoderado,
            NombreApoderado = config.UsarApoderado ? config.NombreApoderado : null,
            CuitApoderado = config.UsarApoderado ? config.CuitApoderado : null,
            PuntoDeVenta = config.PuntoDeVentaActivo!.Numero,
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

    private Task<ServiceResult<CaeResult>> EmitirCaeAsync(Recibo recibo, Configuracion config, CancellationToken ct)
        => _afip.ObtenerCAEAsync(new ComprobanteAfipRequest
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
            return await EnviarAsync(agencia, pdf, $"Recibo_{recibo.ReceptorNombre}_{anio}{mes:00}.pdf",
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
            return await EnviarAsync(agencia, pdf, $"ReciboConsolidado_{recibo.ReceptorNombre}_{anio}{mes:00}.pdf",
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

        // Anular recibo + NC + desvincular vouchers en un único SaveChanges (atómico).
        await _recibos.AnularConNotaAsync(recibo, nota, ct);

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
        if (string.IsNullOrEmpty(recibo.CAE)) return ServiceResult<bool>.Fail("El recibo no tiene CAE: emítalo antes de reenviar.");

        byte[] pdf = recibo.EsConsolidadoVouchers
            ? await _pdf.GenerarPdfDescargaAsync(recibo.Vouchers.ToList(), recibo, ct)
            : await _pdf.GenerarPdfReciboAsync(recibo, ct);

        var error = await EnviarAsync(recibo.Agencia, pdf,
            $"Recibo_{recibo.ReceptorNombre}_{recibo.PeriodoAnio}{recibo.PeriodoMes:00}.pdf",
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
