using PuertoBB.Core.Common;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Models.Resultados;

namespace PuertoBB.Core.Interfaces.Services;

/// <summary>Servicio de recibos de la Cámara Portuaria (emisión, anulación, pagos).</summary>
public interface ICamaraPortuariaReciboService
{
    /// <summary>Empresas del grupo que YA tienen recibo en el período (para advertir antes de emitir).</summary>
    Task<ServiceResult<IReadOnlyList<string>>> GetDuplicadosAsync(int grupoId, int anio, int mes, CancellationToken ct = default);

    /// <summary>Emite un recibo por cada empresa del grupo en el período.</summary>
    Task<ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>> EmitirMasivoAsync(int grupoId, int anio, int mes, CancellationToken ct = default);

    /// <summary>Emite un recibo individual a una empresa (fuera del ciclo masivo).</summary>
    Task<ServiceResult<ResultadoEmisionPorEntidad>> EmitirIndividualAsync(int empresaId, decimal importe, string detalle, int anio, int mes, CancellationToken ct = default);

    /// <summary>Anula un recibo emitido generando una nota de crédito.</summary>
    Task<ServiceResult<bool>> AnularReciboAsync(int reciboId, bool enviarMail, CancellationToken ct = default);

    /// <summary>Reenvía el mail de un recibo (típicamente en estado Emitido).</summary>
    Task<ServiceResult<bool>> ReenviarMailAsync(int reciboId, CancellationToken ct = default);

    /// <summary>Marca un recibo como pagado y registra la fecha.</summary>
    Task<ServiceResult<bool>> MarcarPagadoAsync(int reciboId, CancellationToken ct = default);

    /// <summary>Recibos del dashboard de pendientes según filtro.</summary>
    Task<ServiceResult<IReadOnlyList<Recibo>>> GetPendientesAsync(FiltroPendientes filtro, CancellationToken ct = default);
}
