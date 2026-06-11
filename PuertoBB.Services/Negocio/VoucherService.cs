using Microsoft.Extensions.Logging;
using PuertoBB.Core.Common;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Enums;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Resultados;

namespace PuertoBB.Services.Negocio;

public class VoucherService : IVoucherService
{
    private readonly IVoucherRepository _vouchers;
    private readonly IContadorVoucherRepository _contador;
    private readonly IAgenciaRepository _agencias;
    private readonly IBarcoRepository _barcos;
    private readonly ILogger<VoucherService> _logger;

    public VoucherService(
        IVoucherRepository vouchers,
        IContadorVoucherRepository contador,
        IAgenciaRepository agencias,
        IBarcoRepository barcos,
        ILogger<VoucherService> logger)
    {
        _vouchers = vouchers;
        _contador = contador;
        _agencias = agencias;
        _barcos = barcos;
        _logger = logger;
    }

    public async Task<ServiceResult<Voucher>> CrearVoucherAsync(int agenciaId, int barcoId, DateTime fecha, decimal importe, CancellationToken ct = default)
    {
        if (await _agencias.GetByIdAsync(agenciaId, ct) is null)
            return ServiceResult<Voucher>.Fail("La agencia no existe.");
        if (await _barcos.GetByIdAsync(barcoId, ct) is null)
            return ServiceResult<Voucher>.Fail("El barco no existe.");
        if (importe <= 0)
            return ServiceResult<Voucher>.Fail("El importe debe ser mayor a cero.");

        var numero = await _contador.ObtenerSiguienteNumeroAsync(ct);
        var voucher = new Voucher
        {
            AgenciaId = agenciaId,
            BarcoId = barcoId,
            Numero = numero,
            Importe = importe,
            Fecha = fecha,
            PeriodoAnio = fecha.Year,
            PeriodoMes = fecha.Month
        };

        await _vouchers.AddAsync(voucher, ct);
        _logger.LogInformation("Voucher creado: Nro={Numero} Agencia={AgenciaId} Importe={Importe}", numero, agenciaId, importe);
        return ServiceResult<Voucher>.Ok(voucher);
    }

    public async Task<ServiceResult<IReadOnlyList<Voucher>>> GetPendientesAsync(int anio, int mes, CancellationToken ct = default)
        => ServiceResult<IReadOnlyList<Voucher>>.Ok(await _vouchers.GetPendientesByPeriodoAsync(anio, mes, ct));

    public async Task<ServiceResult<IReadOnlyList<Voucher>>> GetDelPeriodoAsync(int anio, int mes, CancellationToken ct = default)
        => ServiceResult<IReadOnlyList<Voucher>>.Ok(
            (await _vouchers.GetTodosByPeriodoAsync(anio, mes, ct)).OrderBy(v => v.Numero).ToList());

    public async Task<ServiceResult<bool>> ActualizarVoucherAsync(Voucher voucher, CancellationToken ct = default)
    {
        var existente = await _vouchers.GetByIdAsync(voucher.Id, ct);
        if (existente is null) return ServiceResult<bool>.Fail("El voucher no existe.");
        if (existente.ReciboId is not null) return ServiceResult<bool>.Fail("El voucher ya fue consolidado y no puede editarse.");

        if (voucher.AgenciaId > 0) existente.AgenciaId = voucher.AgenciaId;
        existente.BarcoId = voucher.BarcoId;
        existente.Importe = voucher.Importe;
        existente.Fecha = voucher.Fecha;
        existente.PeriodoAnio = voucher.Fecha.Year;
        existente.PeriodoMes = voucher.Fecha.Month;
        await _vouchers.UpdateAsync(existente, ct);
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> EliminarVoucherAsync(int voucherId, CancellationToken ct = default)
    {
        var existente = await _vouchers.GetByIdAsync(voucherId, ct);
        if (existente is null) return ServiceResult<bool>.Fail("El voucher no existe.");
        if (existente.ReciboId is not null) return ServiceResult<bool>.Fail("El voucher ya fue consolidado y no puede eliminarse.");

        await _vouchers.DeleteAsync(voucherId, ct);
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<IReadOnlyList<AgenciaCierrePeriodoVm>>> GetCierrePeriodoAsync(int anio, int mes, CancellationToken ct = default)
    {
        var todos = await _vouchers.GetTodosByPeriodoAsync(anio, mes, ct);

        var agencias = todos
            .GroupBy(v => v.AgenciaId)
            .Select(g =>
            {
                var primero = g.First();
                var nombre = primero.Agencia?.Nombre ?? $"#{g.Key}";

                // Si todos comparten un mismo Recibo consolidado, ese es el recibo de la agencia.
                var recibos = g.Where(v => v.Recibo is not null && v.Recibo.EsConsolidadoVouchers)
                               .Select(v => v.Recibo!)
                               .DistinctBy(r => r.Id)
                               .ToList();
                var reciboConsolidado = recibos.Count == 1 ? recibos[0] : null;

                var estado = MapEstado(reciboConsolidado?.Estado);

                var vouchers = g.OrderBy(v => v.Numero)
                                .Select(v => new VoucherCierreVm(
                                    v.Id, v.Numero,
                                    v.Barco?.Nombre ?? $"#{v.BarcoId}",
                                    v.Fecha, v.Importe))
                                .ToList();

                return new AgenciaCierrePeriodoVm
                {
                    AgenciaId = g.Key,
                    AgenciaNombre = nombre,
                    Vouchers = vouchers,
                    Total = vouchers.Sum(v => v.Importe),
                    Estado = estado,
                    NumeroComprobante = reciboConsolidado?.NumeroComprobante,
                    ReciboId = reciboConsolidado?.Id
                };
            })
            .OrderBy(a => a.AgenciaNombre)
            .ToList();

        return ServiceResult<IReadOnlyList<AgenciaCierrePeriodoVm>>.Ok(agencias);
    }

    private static EstadoCierreAgencia MapEstado(ReciboEstado? estadoRecibo) => estadoRecibo switch
    {
        null                  => EstadoCierreAgencia.Pendiente,
        ReciboEstado.Emitido  => EstadoCierreAgencia.Emitido,
        ReciboEstado.Enviado  => EstadoCierreAgencia.Completo,
        ReciboEstado.Pagado   => EstadoCierreAgencia.Completo,
        // Decisión: un consolidado Anulado vuelve a figurar como Pendiente para permitir reemitir el período.
        // (Si en el futuro se quiere distinguirlo visualmente, agregar un estado "Anulado" a EstadoCierreAgencia.)
        ReciboEstado.Anulado  => EstadoCierreAgencia.Pendiente,
        _                     => EstadoCierreAgencia.Pendiente
    };
}
