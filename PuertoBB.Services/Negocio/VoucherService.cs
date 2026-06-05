using Microsoft.Extensions.Logging;
using PuertoBB.Core.Common;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;

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

    public async Task<ServiceResult<bool>> ActualizarVoucherAsync(Voucher voucher, CancellationToken ct = default)
    {
        var existente = await _vouchers.GetByIdAsync(voucher.Id, ct);
        if (existente is null) return ServiceResult<bool>.Fail("El voucher no existe.");
        if (existente.ReciboId is not null) return ServiceResult<bool>.Fail("El voucher ya fue consolidado y no puede editarse.");

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
}
