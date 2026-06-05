using PuertoBB.Core.Common;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Models.Resultados;

namespace PuertoBB.Core.Interfaces.Services;

/// <summary>Servicio de recibos del Centro Marítimo (cierre de período, emisión, anulación, pagos).</summary>
public interface ICentroMaritimoReciboService
{
    /// <summary>Consolida los vouchers pendientes de cada agencia del período en un recibo.</summary>
    Task<ServiceResult<IReadOnlyList<ResultadoCierrePorAgencia>>> CerrarPeriodoAsync(int anio, int mes, CancellationToken ct = default);

    /// <summary>Agencias del grupo que YA tienen recibo en el período.</summary>
    Task<ServiceResult<IReadOnlyList<string>>> GetDuplicadosAsync(int grupoId, int anio, int mes, CancellationToken ct = default);

    /// <summary>Emite un recibo de cuota por cada agencia del grupo en el período.</summary>
    Task<ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>> EmitirMasivoAsync(int grupoId, int anio, int mes, CancellationToken ct = default);

    /// <summary>Emite un recibo individual a una agencia (cobro extraordinario / puntual).</summary>
    Task<ServiceResult<ResultadoEmisionPorEntidad>> EmitirIndividualAsync(int agenciaId, decimal importe, string detalle, int anio, int mes, CancellationToken ct = default);

    Task<ServiceResult<bool>> AnularReciboAsync(int reciboId, bool enviarMail, CancellationToken ct = default);
    Task<ServiceResult<bool>> ReenviarMailAsync(int reciboId, CancellationToken ct = default);
    Task<ServiceResult<bool>> MarcarPagadoAsync(int reciboId, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<Recibo>>> GetPendientesAsync(FiltroPendientes filtro, CancellationToken ct = default);
}
