using Microsoft.Extensions.Logging;
using PuertoBB.Core.Afip;
using PuertoBB.Core.Common;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Enums;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Mail;
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

    public Task<ServiceResult<IReadOnlyList<ResultadoCierrePorAgencia>>> CerrarPeriodoAsync(int anio, int mes, IProgress<ProgresoMasivo>? progreso = null, CancellationToken ct = default)
        => ProcesarPeriodoAsync(anio, mes, enviarMail: true, progreso, ct);

    public Task<ServiceResult<IReadOnlyList<ResultadoCierrePorAgencia>>> EmitirRecibosPeriodoAsync(int anio, int mes, IProgress<ProgresoMasivo>? progreso = null, CancellationToken ct = default)
        => ProcesarPeriodoAsync(anio, mes, enviarMail: false, progreso, ct);

    private async Task<ServiceResult<IReadOnlyList<ResultadoCierrePorAgencia>>> ProcesarPeriodoAsync(int anio, int mes, bool enviarMail, IProgress<ProgresoMasivo>? progreso, CancellationToken ct)
    {
        var config = await _config.GetAsync(ct);
        if (config.PuntoDeVentaActivo is null)
            return ServiceResult<IReadOnlyList<ResultadoCierrePorAgencia>>.Fail("Configure un punto de venta activo en Configuración.");

        var porAgencia = await GetAgenciasAProcesarAsync(anio, mes, ct);
        var resultados = new List<ResultadoCierrePorAgencia>();
        var total = porAgencia.Count;
        var i = 0;
        foreach (var (agId, vouchersAgencia) in porAgencia)
        {
            ct.ThrowIfCancellationRequested();
            progreso?.Report(new ProgresoMasivo(++i, total, vouchersAgencia.FirstOrDefault()?.Agencia?.Nombre ?? $"Agencia {i}"));
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
                    {
                        await _vouchers.MarcarConsolidadosAsync(vouchersAgencia.Select(v => v.Id), existente.Id, ct);
                        // N-3: los vínculos se guardaron en OTRO DbContext (transient). Recargar para que
                        // la colección Vouchers de ESTE contexto incluya los nuevos (mail y count correctos).
                        existente = await _recibos.GetConsolidadoAsync(agenciaId, anio, mes, ct)
                                    ?? throw new InvalidOperationException("El consolidado desapareció durante el reintento.");
                    }

                    var todosVouchers = existente.Vouchers.OrderBy(v => v.Numero).ToList();
                    existente.Importe = todosVouchers.Sum(v => Round2(v.Importe));
                    existente.Detalle = "Vouchers Nros: " + string.Join(", ", todosVouchers.Select(v => v.Numero));
                    existente.Lineas.Clear();
                    foreach (var (v, i) in todosVouchers.Select((v, i) => (v, i)))
                        existente.Lineas.Add(new ReciboLinea
                        {
                            Descripcion    = $"Voucher {v.Numero} — {v.Barco?.Nombre ?? $"#{v.BarcoId}"} — {Formato.Fecha(v.Fecha)}",
                            Cantidad       = 1,
                            PrecioUnitario = v.Importe,
                            Importe        = Round2(v.Importe),
                            Orden          = i
                        });
                }

                var resExistente = await ProcesarReciboAsync(existente, existente.Agencia, config, enviarMail, forzarEnvio: false, ct);
                return resExistente.Exito
                    ? ResultadoCierrePorAgencia.Ok(agenciaId, agenciaExistente?.Nombre ?? nombreAgencia, existente.Vouchers.Count, existente.Importe, existente.NumeroComprobante, resExistente.ErrorMail)
                    : ResultadoCierrePorAgencia.Fallo(agenciaId, agenciaExistente?.Nombre ?? nombreAgencia, resExistente.ErrorEmision ?? "No se pudo procesar el recibo consolidado.");
            }

            // Caso nuevo: persistir Pendiente ANTES de pedir el CAE.
            var agencia = await _agencias.GetConDetalleAsync(agenciaId, ct);
            if (agencia is null)
                return ResultadoCierrePorAgencia.Fallo(agenciaId, nombreAgencia, "La agencia no existe.");

            var importe = vouchersAgencia.Sum(v => Round2(v.Importe));
            var detalle = "Vouchers Nros: " + string.Join(", ", vouchersAgencia.Select(v => v.Numero));

            var recibo = ConstruirRecibo(agencia, null, importe, detalle, anio, mes, config, esConsolidado: true);
            recibo.EstadoFiscal = EstadoFiscal.Pendiente;

            // Snapshot del detalle: una línea por voucher.
            recibo.Lineas = vouchersAgencia
                .Select((v, i) => new ReciboLinea
                {
                    Descripcion    = $"Voucher {v.Numero} — {v.Barco?.Nombre ?? $"#{v.BarcoId}"} — {Formato.Fecha(v.Fecha)}",
                    Cantidad       = 1,
                    PrecioUnitario = v.Importe,
                    Importe        = Round2(v.Importe),
                    Orden          = i
                })
                .ToList();

            // Persistir recibo + vincular vouchers en un único SaveChanges (atómico).
            await _recibos.AddConVouchersAsync(recibo, vouchersAgencia.Select(v => v.Id).ToList(), ct);

            var resultado = await ProcesarReciboAsync(recibo, agencia, config, enviarMail, forzarEnvio: false, ct);
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
        var importeEsperado = LineasDelGrupo(grupo).Sum(l => Round2(l.Importe)); // total del grupo, para validar antes de emitir
        var lista = grupo.Agencias
            .OrderBy(ag => ag.Agencia?.Nombre)
            .Select(ag => new EstadoEmisionEntidad<Recibo>(ag.AgenciaId, ag.Agencia?.Nombre ?? $"#{ag.AgenciaId}", recibos.GetValueOrDefault(ag.AgenciaId), importeEsperado))
            .ToList();
        return ServiceResult<IReadOnlyList<EstadoEmisionEntidad<Recibo>>>.Ok(lista);
    }

    public async Task<ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>> EmitirMasivoAsync(int grupoId, int anio, int mes, bool enviarMail = true, bool reenviarYaEnviados = false, IProgress<ProgresoMasivo>? progreso = null, CancellationToken ct = default)
    {
        var grupo = await _grupos.GetConMiembrosAsync(grupoId, ct);
        if (grupo is null) return ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>.Fail("El grupo no existe.");

        var config = await _config.GetAsync(ct);
        var lineasGrupo = LineasDelGrupo(grupo);
        var resultados = new List<ResultadoEmisionPorEntidad>();

        var total = grupo.Agencias.Count;
        var i = 0;
        foreach (var ag in grupo.Agencias)
        {
            if (ag.Agencia is not { } agencia) continue; // el join siempre trae la agencia (Include); fija la no-nulabilidad
            ct.ThrowIfCancellationRequested();
            progreso?.Report(new ProgresoMasivo(++i, total, agencia.Nombre));
            resultados.Add(await EmitirOResumirAsync(agencia, grupoId, lineasGrupo, DateTime.Today, anio, mes, config, enviarMail, reenviarYaEnviados, ct));
        }

        return ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>.Ok(resultados);
    }

    public async Task<ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>> EnviarMasivoAsync(int grupoId, int anio, int mes, IProgress<ProgresoMasivo>? progreso = null, CancellationToken ct = default)
    {
        var config = await _config.GetAsync(ct);
        var resultados = new List<ResultadoEmisionPorEntidad>();

        // Una sola query para todos los recibos del grupo en el período (incluye Agencia.Emails + Lineas).
        // Solo se envía lo que ya tiene CAE y aún no fue enviado → materializar para reportar un total exacto.
        var recibos = await _recibos.GetPorGrupoYPeriodoAsync(grupoId, anio, mes, ct);
        var aEnviar = recibos.Where(r => !string.IsNullOrEmpty(r.CAE) && r.EstadoFiscal == EstadoFiscal.Emitido).ToList();
        var total = aEnviar.Count;
        var i = 0;
        foreach (var recibo in aEnviar)
        {
            if (recibo.Agencia is null) continue; // sin la navegación cargada no hay emails para enviar
            ct.ThrowIfCancellationRequested();
            progreso?.Report(new ProgresoMasivo(++i, total, recibo.ReceptorNombre));
            resultados.Add(await ProcesarReciboAsync(recibo, recibo.Agencia, config, enviarMail: true, forzarEnvio: false, ct));
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
        var resultado = await EmitirOResumirAsync(ag.Agencia, grupoId, LineasDelGrupo(grupo), DateTime.Today, anio, mes, config, enviarMail, reenviarYaEnviados: false, ct);
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
        return ServiceResult<ResultadoEmisionPorEntidad>.Ok(await EmitirOResumirAsync(agencia, null, items, fechaEmision, anio, mes, config, enviarMail, reenviarYaEnviados: false, ct));
    }

    public async Task<ServiceResult<ResultadoEmisionPorEntidad>> ReintentarAsync(int reciboId, bool enviarMail, CancellationToken ct = default)
    {
        var recibo = await _recibos.GetConDetalleAsync(reciboId, ct);
        if (recibo is null) return ServiceResult<ResultadoEmisionPorEntidad>.Fail("El recibo no existe.");
        if (recibo.EstadoFiscal == EstadoFiscal.Anulado || recibo.FechaPago is not null || recibo.FechaIncobrable is not null)
            return ServiceResult<ResultadoEmisionPorEntidad>.Fail("El recibo no admite reintento.");

        var config = await _config.GetAsync(ct);
        // Si sigue sin CAE, re-sincronizar el snapshot fiscal del receptor desde el maestro: un recibo
        // trabado en Pendiente por un dato faltante (p. ej. condición IVA RG 5616) ya corregido en
        // Agencias debe tomar el valor nuevo al reintentar. Con CAE ya emitido el snapshot queda congelado.
        if (string.IsNullOrEmpty(recibo.CAE) && recibo.Agencia is { } agencia)
        {
            recibo.ReceptorNombre = agencia.Nombre;
            recibo.ReceptorRazonSocial = agencia.RazonSocial;
            recibo.ReceptorCuit = agencia.Cuit;
            recibo.ReceptorDomicilio = agencia.Domicilio;
            recibo.ReceptorCondicionIva = CatalogoCondicionesIvaReceptor.Descripcion(agencia.CondicionIvaId);
            recibo.ReceptorCondicionIvaId = agencia.CondicionIvaId;
        }
        var resultado = await ProcesarReciboAsync(recibo, recibo.Agencia, config, enviarMail, forzarEnvio: false, ct);
        return ServiceResult<ResultadoEmisionPorEntidad>.Ok(resultado);
    }

    public async Task<ServiceResult<bool>> EditarReciboPendienteAsync(int reciboId, IReadOnlyList<ReciboLineaInput> lineas, CancellationToken ct = default)
    {
        if (lineas.Count == 0) return ServiceResult<bool>.Fail("El recibo debe tener al menos un ítem.");
        var recibo = await _recibos.GetConDetalleAsync(reciboId, ct);
        if (recibo is null) return ServiceResult<bool>.Fail("El recibo no existe.");
        if (!string.IsNullOrEmpty(recibo.CAE))
            return ServiceResult<bool>.Fail("El recibo ya tiene CAE: no se puede editar.");

        // Re-sincroniza el snapshot del recibo Pendiente con las nuevas líneas (igual que el reintento del grupo).
        recibo.Importe = lineas.Sum(l => Round2(l.Importe));
        recibo.Detalle = string.Join(" · ", lineas.Select(l => l.Descripcion));
        recibo.Lineas.Clear();
        foreach (var (l, i) in lineas.Select((l, i) => (l, i)))
            recibo.Lineas.Add(new ReciboLinea { Descripcion = l.Descripcion, Cantidad = l.Cantidad, PrecioUnitario = l.PrecioUnitario, Importe = Round2(l.Importe), Orden = i });
        await _recibos.UpdateAsync(recibo, ct);
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> EliminarReciboPendienteAsync(int reciboId, CancellationToken ct = default)
    {
        var recibo = await _recibos.GetConDetalleAsync(reciboId, ct);
        if (recibo is null) return ServiceResult<bool>.Fail("El recibo no existe.");
        if (!string.IsNullOrEmpty(recibo.CAE))
            return ServiceResult<bool>.Fail("El recibo ya tiene CAE: anúlelo en vez de eliminarlo.");
        await _recibos.EliminarPendienteAsync(reciboId, ct);
        return ServiceResult<bool>.Ok(true);
    }

    /// <summary>
    /// Crea (en estado Pendiente, persistido antes del CAE) o resume un recibo de cuota del período, y
    /// avanza su emisión idempotentemente. Si ya está completo, devuelve "ya existe".
    /// </summary>
    private async Task<ResultadoEmisionPorEntidad> EmitirOResumirAsync(
        Agencia agencia, int? grupoId, IReadOnlyList<ReciboLineaInput> lineas, DateTime fechaEmision, int anio, int mes, Configuracion config, bool enviarMail, bool reenviarYaEnviados, CancellationToken ct)
    {
        if (config.PuntoDeVentaActivo is null)
            return ResultadoEmisionPorEntidad.Fallo(agencia.Id, agencia.Nombre, "Configure un punto de venta activo en Configuración.");

        try
        {
            var importe = lineas.Sum(l => Round2(l.Importe));
            var detalle = string.Join(" · ", lineas.Select(l => l.Descripcion));
            var recibo = await _recibos.GetPorClaveAsync(agencia.Id, grupoId, anio, mes, ct);
            // N-1: para individuales, un Pendiente con contenido DISTINTO no es un reintento — es otro
            // cobro. Crear un recibo nuevo en lugar de pisar el snapshot (D-20 permite N por período).
            if (recibo is not null && grupoId is null && !MismoContenido(recibo, lineas))
                recibo = null;
            if (recibo is null)
            {
                recibo = ConstruirReciboPendiente(agencia, grupoId, importe, detalle, fechaEmision, anio, mes, config);
                // Snapshot multi-ítem (reemplaza la línea única por defecto que arma ConstruirRecibo).
                recibo.Lineas = lineas.Select((l, i) => new ReciboLinea { Descripcion = l.Descripcion, Cantidad = l.Cantidad, PrecioUnitario = l.PrecioUnitario, Importe = Round2(l.Importe), Orden = i }).ToList();
                await _recibos.AddAsync(recibo, ct);
            }
            else
            {
                agencia = recibo.Agencia; // rastreada, con emails
                // Ya emitido y enviado: NO es error. Reenviar solo si se pidió explícitamente; si no, omitir.
                if (EsCompleto(recibo))
                    return reenviarYaEnviados && enviarMail
                        ? await ProcesarReciboAsync(recibo, agencia, config, enviarMail: true, forzarEnvio: true, ct)
                        : ResultadoEmisionPorEntidad.Omitida(agencia.Id, agencia.Nombre, "Ya emitido y enviado.");

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
                    recibo.ReceptorCondicionIva = CatalogoCondicionesIvaReceptor.Descripcion(agencia.CondicionIvaId);
                    recibo.ReceptorCondicionIvaId = agencia.CondicionIvaId;
                    recibo.Lineas.Clear();
                    foreach (var (l, i) in lineas.Select((l, i) => (l, i)))
                        recibo.Lineas.Add(new ReciboLinea { Descripcion = l.Descripcion, Cantidad = l.Cantidad, PrecioUnitario = l.PrecioUnitario, Importe = Round2(l.Importe), Orden = i });
                }
            }

            return await ProcesarReciboAsync(recibo, agencia, config, enviarMail, forzarEnvio: false, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al emitir cuota para Agencia={AgenciaId}", agencia.Id);
            return ResultadoEmisionPorEntidad.Fallo(agencia.Id, agencia.Nombre, ex.Message);
        }
    }

    /// <summary>Un recibo está "completo" cuando ya no hay nada que reintentar (lógica compartida CP/CM).</summary>
    private static bool EsCompleto(Recibo r) => EstadoReciboHelper.EsCompleto(r);

    /// <summary>Redondea a 2 decimales (igual que el mapper AFIP) para que la suma de líneas
    /// persistidas coincida exactamente con el total que se envía a AFIP.</summary>
    private static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <summary>True si el recibo tiene exactamente las mismas líneas que el pedido (mismo cobro).</summary>
    private static bool MismoContenido(Recibo r, IReadOnlyList<ReciboLineaInput> lineas)
    {
        if (r.Lineas.Count != lineas.Count) return false;
        var actuales = r.Lineas.OrderBy(l => l.Orden).ToList();
        for (var i = 0; i < lineas.Count; i++)
            if (actuales[i].Descripcion != lineas[i].Descripcion ||
                actuales[i].Cantidad != lineas[i].Cantidad ||
                actuales[i].PrecioUnitario != lineas[i].PrecioUnitario)
                return false;
        return true;
    }

    /// <summary>
    /// Avanza un recibo persistido: pide el CAE solo si falta (idempotente), y manda el mail si
    /// corresponde. Para recibos consolidados arma el PDF de descarga; para cuotas, el recibo simple.
    /// </summary>
    private async Task<ResultadoEmisionPorEntidad> ProcesarReciboAsync(
        Recibo recibo, Agencia agencia, Configuracion config, bool enviarMail, bool forzarEnvio, CancellationToken ct)
    {
        // 1. CAE (idempotente: no se vuelve a pedir si ya está).
        if (string.IsNullOrEmpty(recibo.CAE))
        {
            // Recuperación anti-duplicado: si un intento previo falló (UltimoErrorCae), el CAE pudo
            // haberse autorizado sin que lo registráramos (crash/cancelación tras la autorización).
            // Recuperarlo de AFIP antes de re-emitir evita un segundo comprobante.
            if (!string.IsNullOrEmpty(recibo.UltimoErrorCae) && recibo.ReceptorCondicionIvaId is > 0)
            {
                var rec = await _afip.RecuperarComprobanteAsync(ConstruirAfipRequest(recibo), ct);
                if (rec is { Success: true, Data: { } recuperado }
                    && !await _recibos.ExisteComprobanteAsync(recibo.PuntoDeVenta, recibo.CodigoAfip, recuperado.NumeroComprobante, ct))
                {
                    AplicarCae(recibo, recuperado);
                    recibo.UltimoErrorCae = null;
                    recibo.EstadoFiscal = EstadoFiscal.Emitido;
                    _logger.LogWarning("CAE recuperado de AFIP: PV {Pv} Nro {Nro} CAE {Cae} (recibo {Id}) — persistiendo…",
                        recibo.PuntoDeVenta, recibo.NumeroComprobante, recibo.CAE, recibo.Id);
                    await _recibos.UpdateAsync(recibo, ct);
                    return ResultadoEmisionPorEntidad.Ok(agencia.Id, agencia.Nombre, recibo.NumeroComprobante, recibo.UltimoErrorMail);
                }
            }

            var cae = await EmitirCaeAsync(recibo, config, ct);
            if (!cae.Success || cae.Data is null)
            {
                recibo.EstadoFiscal = EstadoFiscal.Pendiente;
                recibo.UltimoErrorCae = cae.ErrorMessage ?? "AFIP no devolvió CAE.";
                await _recibos.UpdateAsync(recibo, ct);
                return ResultadoEmisionPorEntidad.Fallo(agencia.Id, agencia.Nombre, recibo.UltimoErrorCae);
            }
            AplicarCae(recibo, cae.Data);
            recibo.UltimoErrorCae = null;
            recibo.EstadoFiscal = EstadoFiscal.Emitido;
            // F: rastro del CAE autorizado ANTES de persistir (único registro si el guardado falla).
            _logger.LogWarning("CAE autorizado por AFIP: PV {Pv} Nro {Nro} CAE {Cae} (recibo {Id}) — persistiendo…",
                recibo.PuntoDeVenta, recibo.NumeroComprobante, recibo.CAE, recibo.Id);
            await _recibos.UpdateAsync(recibo, ct);
        }

        // 2. Mail (opcional; un fallo NO revierte la emisión). "Enviado" se deriva de FechaEnvioMail.
        // forzarEnvio = reenvío explícito de un recibo ya enviado (pedido por el usuario en el lote).
        if (enviarMail && (forzarEnvio || recibo.FechaEnvioMail is null))
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
            ReceptorCondicionIva = CatalogoCondicionesIvaReceptor.Descripcion(agencia.CondicionIvaId),
            ReceptorCondicionIvaId = agencia.CondicionIvaId,
            PeriodoAnio = anio,
            PeriodoMes = mes,
            Importe = importe,
            Detalle = detalle,
            Lineas = [new ReciboLinea { Descripcion = detalle, Cantidad = 1, PrecioUnitario = importe, Importe = importe, Orden = 0 }],
            EsConsolidadoVouchers = esConsolidado,
            PuntoDeVenta = config.PuntoDeVentaActivo!.Numero,
            TipoComprobante = TipoComprobante.Recibo,
            CodigoAfip = config.CodigoAfipRecibo,
            FechaEmision = hoy,
            FechaVencimientoPago = hoy.AddDays(config.DiasVencimiento),
            // Default seguro: un recibo recién construido nunca está Emitido sin CAE (los callers
            // que corresponda lo avanzan tras obtener el CAE). Evita la trampa de "Emitido sin CAE".
            EstadoFiscal = EstadoFiscal.Pendiente
        };
    }

    /// <summary>Recibo de cuota nuevo en estado Pendiente (sin CAE), con la fecha de emisión indicada.</summary>
    private Recibo ConstruirReciboPendiente(Agencia agencia, int? grupoId, decimal importe, string detalle, DateTime fechaEmision, int anio, int mes, Configuracion config)
    {
        var recibo = ConstruirRecibo(agencia, grupoId, importe, detalle, anio, mes, config, esConsolidado: false);
        recibo.FechaEmision = fechaEmision;
        recibo.FechaVencimientoPago = fechaEmision.AddDays(config.DiasVencimiento);
        recibo.EstadoFiscal = EstadoFiscal.Pendiente;
        return recibo;
    }

    private Task<ServiceResult<CaeResult>> EmitirCaeAsync(Recibo recibo, Configuracion config, CancellationToken ct)
    {
        // RG 5616: sin condición de IVA del receptor AFIP rechaza con 10242; cortar antes de llamar.
        if (recibo.ReceptorCondicionIvaId is not (> 0))
            return Task.FromResult(ServiceResult<CaeResult>.Fail(
                $"'{recibo.ReceptorNombre}' no tiene asignada la condición frente al IVA (obligatoria por RG 5616). Asígnela en Agencias y reintente."));

        return _afip.ObtenerCAEAsync(ConstruirAfipRequest(recibo), ct);
    }

    /// <summary>Arma el request AFIP de un recibo (requiere ReceptorCondicionIvaId &gt; 0, validado por el caller).</summary>
    private static ComprobanteAfipRequest ConstruirAfipRequest(Recibo recibo) => new()
    {
        TipoComprobante = TipoComprobante.Recibo,
        CodigoAfip = recibo.CodigoAfip,
        PuntoDeVenta = recibo.PuntoDeVenta,
        CuitReceptor = PeriodoHelper.SoloDigitos(recibo.ReceptorCuit),
        CondicionIvaReceptorId = recibo.ReceptorCondicionIvaId!.Value,
        ImporteTotal = recibo.Importe,
        FechaEmision = recibo.FechaEmision,
        PeriodoServicioDesde = PeriodoHelper.PrimerDia(recibo.PeriodoAnio, recibo.PeriodoMes),
        PeriodoServicioHasta = PeriodoHelper.UltimoDia(recibo.PeriodoAnio, recibo.PeriodoMes),
        FechaVencimientoPago = recibo.FechaVencimientoPago
    };

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
            var (asunto, texto, html) = await ArmarMailAsync("Recibo", recibo, anio, mes, ct);
            return await EnviarAsync(agencia, pdf, $"Recibo_{recibo.ReceptorNombre}_{anio}{mes:00}.pdf", asunto, texto, html, ct);
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
            var (asunto, texto, html) = await ArmarMailAsync("Recibo consolidado", recibo, anio, mes, ct);
            return await EnviarAsync(agencia, pdf, $"ReciboConsolidado_{recibo.ReceptorNombre}_{anio}{mes:00}.pdf", asunto, texto, html, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falló PDF/mail consolidado para Agencia={AgenciaId}", agencia.Id);
            return ex.Message;
        }
    }

    private async Task<string?> EnviarAsync(Agencia agencia, byte[] pdf, string nombre, string asunto, string cuerpoTexto, string? cuerpoHtml, CancellationToken ct)
    {
        var emails = agencia.Emails.Where(e => e.Activo).Select(e => e.Email).ToList();
        var envio = await _mail.EnviarReciboAsync(emails, pdf, nombre, asunto, cuerpoTexto, cuerpoHtml, ct);
        return envio.Success ? null : envio.ErrorMessage;
    }

    /// <summary>Asunto y cuerpo (texto + HTML) desde la plantilla, con las variables del recibo resueltas.</summary>
    private Task<(string Asunto, string Texto, string? Html)> ArmarMailAsync(string comprobante, Recibo recibo, int anio, int mes, CancellationToken ct)
        => ArmarMailAsync(comprobante, recibo.ReceptorNombre, anio, mes,
               recibo.NumeroComprobante > 0 ? Formato.Comprobante(recibo.PuntoDeVenta, recibo.NumeroComprobante) : null,
               recibo.Importe, ct);

    /// <summary>Variante con número/importe explícitos (p. ej. nota de crédito, cuyo número es el de la nota).</summary>
    private async Task<(string Asunto, string Texto, string? Html)> ArmarMailAsync(
        string comprobante, string? receptor, int anio, int mes, string? numero, decimal importe, CancellationToken ct)
    {
        var cfg = await _config.GetSinTrackingAsync(ct);
        var vars = new Dictionary<string, string?>
        {
            ["periodo"]     = Formato.Periodo(anio, mes),
            ["receptor"]    = receptor,
            ["razonSocial"] = cfg.RazonSocial,
            ["comprobante"] = comprobante,
            ["numero"]      = numero,
            ["importe"]     = Formato.Moneda(importe),
        };
        var asunto = PlantillaMail.Aplicar(string.IsNullOrWhiteSpace(cfg.MailAsunto) ? PlantillaMail.DefaultAsunto : cfg.MailAsunto, vars);
        string texto;
        string? html;
        if (cfg.MailCuerpoEsHtml && !string.IsNullOrWhiteSpace(cfg.MailCuerpo))
        {
            html  = PlantillaMail.Aplicar(cfg.MailCuerpo, vars);
            texto = PlantillaMail.QuitarHtml(html);   // alternativa de texto plano
        }
        else
        {
            texto = PlantillaMail.Aplicar(string.IsNullOrWhiteSpace(cfg.MailCuerpo) ? PlantillaMail.DefaultCuerpoTexto : cfg.MailCuerpo, vars);
            html  = null;
        }
        return (asunto, texto, html);
    }

    public async Task<ServiceResult<ResultadoAnulacion>> AnularReciboAsync(int reciboId, bool enviarMail, CancellationToken ct = default)
    {
        var recibo = await _recibos.GetConDetalleAsync(reciboId, ct);
        if (recibo is null) return ServiceResult<ResultadoAnulacion>.Fail("El recibo no existe.");
        if (recibo.EstadoFiscal == EstadoFiscal.Anulado) return ServiceResult<ResultadoAnulacion>.Fail("El recibo ya está anulado.");
        if (string.IsNullOrEmpty(recibo.CAE))
            return ServiceResult<ResultadoAnulacion>.Fail("El recibo no tiene CAE: emítalo antes de anularlo.");

        var config = await _config.GetAsync(ct);
        if (config.PuntoDeVentaActivo is null)
            return ServiceResult<ResultadoAnulacion>.Fail("Configure un punto de venta activo en Configuración.");
        // La NC debe emitirse desde el mismo PV que el recibo original (su serie/certificado). El PV
        // activo provee el certificado de autenticación; si no coincide, no se anula sin cambiarlo.
        if (recibo.PuntoDeVenta != config.PuntoDeVentaActivo.Numero)
            return ServiceResult<ResultadoAnulacion>.Fail(
                $"Para anular este recibo, activá el punto de venta {recibo.PuntoDeVenta} en Configuración (el recibo se emitió desde ese PV).");
        var hoy = DateTime.Today;

        // RG 5616: condición IVA del snapshot; recibos viejos sin snapshot usan la actual de la agencia.
        var condicionIvaId = recibo.ReceptorCondicionIvaId ?? recibo.Agencia.CondicionIvaId;
        if (condicionIvaId is not (> 0))
            return ServiceResult<ResultadoAnulacion>.Fail(
                "El recibo no registra la condición frente al IVA del receptor: asígnela a la agencia en el ABM y reintente.");

        var afipReq = new ComprobanteAfipRequest
        {
            TipoComprobante = TipoComprobante.NotaDeCredito,
            CodigoAfip = config.CodigoAfipNotaDeCredito,
            PuntoDeVenta = recibo.PuntoDeVenta,
            CuitReceptor = PeriodoHelper.SoloDigitos(recibo.ReceptorCuit),
            CondicionIvaReceptorId = condicionIvaId.Value,
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

        // Recuperación anti-duplicado: si la respuesta del CAE de una NC previa se perdió, AFIP pudo
        // haberla autorizado igual. Recuperarla antes de re-emitir evita una segunda NC sobre el mismo
        // recibo. Confiable dentro del mismo día (la coincidencia incluye la fecha del comprobante);
        // para reintentos en días posteriores queda el aviso de "posible emisión" de ObtenerCAEAsync.
        CaeResult? caeNota = null;
        var recuperada = await _afip.RecuperarComprobanteAsync(afipReq, ct);
        if (recuperada is { Success: true, Data: { } ncRec }
            && !await _recibos.ExisteComprobanteAsync(afipReq.PuntoDeVenta, afipReq.CodigoAfip, ncRec.NumeroComprobante, ct))
        {
            _logger.LogWarning("NC recuperada de AFIP: PV {Pv} Nro {Numero} CAE {Cae} (recibo {ReciboId}) — adoptando…",
                afipReq.PuntoDeVenta, ncRec.NumeroComprobante, ncRec.Cae, recibo.Id);
            caeNota = ncRec;
        }

        if (caeNota is null)
        {
            var cae = await _afip.ObtenerCAEAsync(afipReq, ct);
            if (!cae.Success || cae.Data is null)
                return ServiceResult<ResultadoAnulacion>.Fail(cae.ErrorMessage ?? "AFIP no devolvió CAE para la nota de crédito.");
            caeNota = cae.Data;
        }

        var nota = new NotaDeCredito
        {
            ReciboOriginalId = recibo.Id,
            ReciboOriginal = recibo,
            PuntoDeVenta = recibo.PuntoDeVenta,
            TipoComprobante = TipoComprobante.NotaDeCredito,
            CodigoAfip = config.CodigoAfipNotaDeCredito,
            NumeroComprobante = caeNota.NumeroComprobante,
            CAE = caeNota.Cae,
            FechaVencimientoCAE = caeNota.FechaVencimientoCae,
            FechaEmision = hoy
        };

        // N-2: si la persistencia local fallara, este log es el único registro del comprobante autorizado.
        _logger.LogWarning("NC autorizada por AFIP: PV {Pv} Nro {Numero} CAE {Cae} (recibo {ReciboId}) — persistiendo…",
            nota.PuntoDeVenta, nota.NumeroComprobante, nota.CAE, recibo.Id);

        // Anular recibo + NC + desvincular vouchers en un único SaveChanges (atómico).
        await _recibos.AnularConNotaAsync(recibo, nota, ct);

        string? errorMail = null;
        if (enviarMail)
        {
            try
            {
                var pdf = await _pdf.GenerarPdfNotaDeCreditoAsync(nota, ct);
                var (asuntoNc, textoNc, htmlNc) = await ArmarMailAsync("Nota de crédito", recibo.ReceptorNombre,
                    recibo.PeriodoAnio, recibo.PeriodoMes, Formato.Comprobante(nota.PuntoDeVenta, nota.NumeroComprobante), recibo.Importe, ct);
                errorMail = await EnviarAsync(recibo.Agencia, pdf,
                    $"NotaCredito_{Formato.Comprobante(nota.PuntoDeVenta, nota.NumeroComprobante)}.pdf",
                    asuntoNc, textoNc, htmlNc, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falló envío de NC del recibo {ReciboId}", reciboId);
                errorMail = ex.Message;
            }

            // Persistir la traza del mail de la NC en el recibo (anulado), igual que el recibo normal:
            // así queda registro de si la NC se envió, incluso tras reiniciar la app.
            recibo.UltimoErrorMail = errorMail;
            recibo.FechaEnvioMail = errorMail is null ? DateTime.Now : null;
            await _recibos.UpdateAsync(recibo, ct);
        }

        return ServiceResult<ResultadoAnulacion>.Ok(
            ResultadoAnulacion.Ok(nota.PuntoDeVenta, nota.NumeroComprobante, errorMail));
    }

    public async Task<ServiceResult<bool>> ReenviarMailAsync(int reciboId, CancellationToken ct = default)
    {
        var recibo = await _recibos.GetConDetalleAsync(reciboId, ct);
        if (recibo is null) return ServiceResult<bool>.Fail("El recibo no existe.");
        if (string.IsNullOrEmpty(recibo.CAE)) return ServiceResult<bool>.Fail("El recibo no tiene CAE: emítalo antes de reenviar.");

        if (recibo.EstadoFiscal == EstadoFiscal.Anulado)
        {
            if (recibo.NotaDeCredito is null)
                return ServiceResult<bool>.Fail("El recibo está anulado pero no tiene nota de crédito registrada.");

            var pdfNota = await _pdf.GenerarPdfNotaDeCreditoAsync(recibo.NotaDeCredito, ct);
            var (asuntoNc, textoNc, htmlNc) = await ArmarMailAsync("Nota de crédito", recibo.ReceptorNombre,
                recibo.PeriodoAnio, recibo.PeriodoMes,
                Formato.Comprobante(recibo.NotaDeCredito.PuntoDeVenta, recibo.NotaDeCredito.NumeroComprobante), recibo.Importe, ct);
            var errorNota = await EnviarAsync(recibo.Agencia, pdfNota,
                $"NotaCredito_{Formato.Comprobante(recibo.NotaDeCredito.PuntoDeVenta, recibo.NotaDeCredito.NumeroComprobante)}.pdf",
                asuntoNc, textoNc, htmlNc, ct);
            recibo.UltimoErrorMail = errorNota;
            recibo.FechaEnvioMail = errorNota is null ? DateTime.Now : null;
            await _recibos.UpdateAsync(recibo, ct);
            return errorNota is null ? ServiceResult<bool>.Ok(true) : ServiceResult<bool>.Fail(errorNota);
        }

        byte[] pdf = recibo.EsConsolidadoVouchers
            ? await _pdf.GenerarPdfDescargaAsync(recibo.Vouchers.ToList(), recibo, ct)
            : await _pdf.GenerarPdfReciboAsync(recibo, ct);

        var comprobante = recibo.EsConsolidadoVouchers ? "Recibo consolidado" : "Recibo";
        var (asunto, texto, html) = await ArmarMailAsync(comprobante, recibo, recibo.PeriodoAnio, recibo.PeriodoMes, ct);
        var error = await EnviarAsync(recibo.Agencia, pdf,
            $"Recibo_{recibo.ReceptorNombre}_{recibo.PeriodoAnio}{recibo.PeriodoMes:00}.pdf",
            asunto, texto, html, ct);

        if (error is not null) return ServiceResult<bool>.Fail(error);

        if (recibo.EstadoFiscal == EstadoFiscal.Emitido)
        {
            recibo.FechaEnvioMail = DateTime.Now;
            recibo.UltimoErrorMail = null;
            await _recibos.UpdateAsync(recibo, ct);
        }
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> MarcarPagadoAsync(int reciboId, CancellationToken ct = default)
    {
        var recibo = await _recibos.GetByIdAsync(reciboId, ct);
        if (recibo is null) return ServiceResult<bool>.Fail("El recibo no existe.");
        if (recibo.EstadoFiscal == EstadoFiscal.Anulado) return ServiceResult<bool>.Fail("No se puede marcar como pagado un recibo anulado.");
        if (recibo.EstadoFiscal == EstadoFiscal.Pendiente) return ServiceResult<bool>.Fail("El recibo aún no tiene CAE: no se puede marcar como pagado.");
        if (recibo.FechaIncobrable is not null) return ServiceResult<bool>.Fail("El recibo está marcado como incobrable: quite la baja antes de marcar pagado.");
        if (recibo.FechaPago is not null) return ServiceResult<bool>.Fail("El recibo ya está pagado.");

        recibo.FechaPago = DateTime.Today;
        await _recibos.UpdateAsync(recibo, ct);
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> MarcarIncobrableAsync(int reciboId, string? motivo, CancellationToken ct = default)
    {
        var recibo = await _recibos.GetByIdAsync(reciboId, ct);
        if (recibo is null) return ServiceResult<bool>.Fail("El recibo no existe.");
        if (recibo.EstadoFiscal != EstadoFiscal.Emitido)
            return ServiceResult<bool>.Fail("Solo un recibo emitido (con CAE y no anulado) puede marcarse como incobrable.");
        if (recibo.FechaPago is not null) return ServiceResult<bool>.Fail("El recibo ya está pagado: no se puede marcar como incobrable.");
        if (recibo.FechaIncobrable is not null) return ServiceResult<bool>.Fail("El recibo ya está marcado como incobrable.");

        recibo.FechaIncobrable = DateTime.Today;
        recibo.MotivoIncobrable = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
        await _recibos.UpdateAsync(recibo, ct);
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> QuitarIncobrableAsync(int reciboId, CancellationToken ct = default)
    {
        var recibo = await _recibos.GetByIdAsync(reciboId, ct);
        if (recibo is null) return ServiceResult<bool>.Fail("El recibo no existe.");
        if (recibo.FechaIncobrable is null) return ServiceResult<bool>.Fail("El recibo no está marcado como incobrable.");

        recibo.FechaIncobrable = null;
        recibo.MotivoIncobrable = null;
        await _recibos.UpdateAsync(recibo, ct);
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<IReadOnlyList<Recibo>>> GetPendientesAsync(FiltroPendientes filtro, CancellationToken ct = default)
        => ServiceResult<IReadOnlyList<Recibo>>.Ok(await _recibos.GetPendientesAsync(filtro, ct));
}
