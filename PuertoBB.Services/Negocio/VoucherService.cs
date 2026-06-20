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
                var nombre = g.First().Agencia?.Nombre ?? $"#{g.Key}";

                // Consolidados (no anulados) de la agencia en el período: original + complementarios (0..n).
                var recibos = g.Where(v => v.Recibo is { EsConsolidadoVouchers: true } r && r.EstadoFiscal != EstadoFiscal.Anulado)
                               .Select(v => v.Recibo!)
                               .DistinctBy(r => r.Id)
                               .ToList();

                // Cantidad de vouchers por recibo, para el detalle.
                var porRecibo = g.Where(v => v.ReciboId is not null)
                                 .GroupBy(v => v.ReciboId!.Value)
                                 .ToDictionary(x => x.Key, x => x.Count());

                var consolidados = recibos
                    .OrderBy(r => r.NumeroComprobante)
                    .Select(r => new ConsolidadoCierreVm(
                        r.Id, r.NumeroComprobante, r.Importe, porRecibo.GetValueOrDefault(r.Id), MapEstado(r)))
                    .ToList();

                // Pendiente si hay vouchers libres por consolidar (1ª emisión o complementario) o un consolidado sin CAE.
                var hayLibres = g.Any(v => v.ReciboId is null);
                var hayPendienteSinCae = consolidados.Any(c => c.Estado == EstadoCierreAgencia.Pendiente);
                var estado =
                      hayLibres || hayPendienteSinCae                                                   ? EstadoCierreAgencia.Pendiente
                    : consolidados.Count > 0 && consolidados.All(c => c.Estado == EstadoCierreAgencia.Completo) ? EstadoCierreAgencia.Completo
                    :                                                                                      EstadoCierreAgencia.Emitido;

                var vouchers = g.OrderBy(v => v.Numero)
                                .Select(v => new VoucherCierreVm(
                                    v.Id, v.Numero,
                                    v.Barco?.Nombre ?? $"#{v.BarcoId}",
                                    v.Fecha, v.Importe,
                                    Libre: v.ReciboId is null,
                                    NumeroComprobante: v.Recibo is { NumeroComprobante: > 0 } ? v.Recibo.NumeroComprobante : null))
                                .ToList();

                return new AgenciaCierrePeriodoVm
                {
                    AgenciaId = g.Key,
                    AgenciaNombre = nombre,
                    Vouchers = vouchers,
                    Total = vouchers.Sum(v => v.Importe),
                    Estado = estado,
                    Consolidados = consolidados
                };
            })
            .OrderBy(a => a.AgenciaNombre)
            .ToList();

        return ServiceResult<IReadOnlyList<AgenciaCierrePeriodoVm>>.Ok(agencias);
    }

    // Estado derivado de un recibo consolidado (no es un estado persistido paralelo): eje fiscal + si ya
    // se envió el mail o se cobró. "Completo" = enviado o pagado.
    private static EstadoCierreAgencia MapEstado(Recibo recibo) => recibo switch
    {
        { EstadoFiscal: EstadoFiscal.Anulado }    => EstadoCierreAgencia.Pendiente,
        { EstadoFiscal: EstadoFiscal.Pendiente }  => EstadoCierreAgencia.Pendiente,
        { FechaEnvioMail: not null }              => EstadoCierreAgencia.Completo,
        { FechaPago: not null }                   => EstadoCierreAgencia.Completo,
        _                                         => EstadoCierreAgencia.Emitido
    };
}
