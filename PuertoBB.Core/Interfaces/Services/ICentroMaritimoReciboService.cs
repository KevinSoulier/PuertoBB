using PuertoBB.Core.Common;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Models;
using PuertoBB.Core.Models.Resultados;

namespace PuertoBB.Core.Interfaces.Services;

/// <summary>Servicio de recibos del Centro Marítimo (cierre de período, emisión, anulación, pagos).</summary>
public interface ICentroMaritimoReciboService
{
    /// <summary>Consolida los vouchers pendientes de cada agencia del período en un recibo.
    /// <paramref name="progreso"/> recibe el avance por agencia para el overlay de espera.</summary>
    Task<ServiceResult<IReadOnlyList<ResultadoCierrePorAgencia>>> CerrarPeriodoAsync(int anio, int mes, IProgress<ProgresoMasivo>? progreso = null, CancellationToken ct = default);

    /// <summary>Consolida los vouchers pendientes de UNA agencia del período en un recibo (reintento individual).</summary>
    Task<ServiceResult<ResultadoCierrePorAgencia>> CerrarPeriodoAgenciaAsync(int agenciaId, int anio, int mes, CancellationToken ct = default);

    /// <summary>Emite el recibo AFIP de una agencia SIN enviar mail.</summary>
    Task<ServiceResult<ResultadoCierrePorAgencia>> EmitirReciboAgenciaAsync(int agenciaId, int anio, int mes, CancellationToken ct = default);

    /// <summary>Emite recibos AFIP para todas las agencias pendientes del período SIN enviar mails.
    /// <paramref name="progreso"/> recibe el avance por agencia para el overlay de espera.</summary>
    Task<ServiceResult<IReadOnlyList<ResultadoCierrePorAgencia>>> EmitirRecibosPeriodoAsync(int anio, int mes, IProgress<ProgresoMasivo>? progreso = null, CancellationToken ct = default);

    /// <summary>Agencias del grupo que YA tienen recibo en el período.</summary>
    Task<ServiceResult<IReadOnlyList<string>>> GetDuplicadosAsync(int grupoId, int anio, int mes, CancellationToken ct = default);

    /// <summary>
    /// Estado de cada agencia del grupo en el período (alimenta la tabla de emisión masiva):
    /// el recibo es null si la agencia aún no fue emitida.
    /// </summary>
    Task<ServiceResult<IReadOnlyList<EstadoEmisionEntidad<Recibo>>>> GetEstadoMasivoAsync(int grupoId, int anio, int mes, CancellationToken ct = default);

    /// <summary>
    /// Emite un recibo de cuota por cada agencia pendiente del grupo en el período. Si <paramref name="enviarMail"/>
    /// es false solo obtiene el CAE ("Emitir"); si es true además envía el mail ("Emitir y enviar").
    /// Es idempotente: los recibos ya completos (emitidos y enviados) se OMITEN (no fallan). Si
    /// <paramref name="reenviarYaEnviados"/> es true y <paramref name="enviarMail"/> también, además reenvía
    /// el mail de los ya enviados. <paramref name="progreso"/> recibe el avance por agencia.
    /// </summary>
    Task<ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>> EmitirMasivoAsync(int grupoId, int anio, int mes, bool enviarMail = true, bool reenviarYaEnviados = false, IProgress<ProgresoMasivo>? progreso = null, CancellationToken ct = default);

    /// <summary>Envía por mail los recibos del grupo que ya tienen CAE y aún no fueron enviados ("Enviar").</summary>
    Task<ServiceResult<IReadOnlyList<ResultadoEmisionPorEntidad>>> EnviarMasivoAsync(int grupoId, int anio, int mes, IProgress<ProgresoMasivo>? progreso = null, CancellationToken ct = default);

    /// <summary>Emite/continúa el recibo de cuota de UNA agencia del grupo en el período (acción por fila).</summary>
    Task<ServiceResult<ResultadoEmisionPorEntidad>> EmitirDeGrupoAsync(int grupoId, int agenciaId, int anio, int mes, bool enviarMail, CancellationToken ct = default);

    /// <summary>
    /// Emite un recibo individual a una agencia (cobro extraordinario / puntual).
    /// Persiste el recibo antes de pedir el CAE (idempotente). Si <paramref name="enviarMail"/>
    /// es false solo genera el CAE; el mail se manda luego con <see cref="ReintentarAsync"/>.
    /// Si <paramref name="lineas"/> trae ítems, el recibo es multi-ítem (total = suma) y <paramref name="importe"/>/<paramref name="detalle"/> se ignoran.
    /// </summary>
    Task<ServiceResult<ResultadoEmisionPorEntidad>> EmitirIndividualAsync(int agenciaId, decimal importe, string detalle, DateTime fechaEmision, int anio, int mes, bool enviarMail, IReadOnlyList<ReciboLineaInput>? lineas = null, CancellationToken ct = default);

    /// <summary>
    /// Reintenta/continúa la emisión de un recibo existente de forma idempotente: pide el CAE solo si
    /// falta, y manda el mail si <paramref name="enviarMail"/> es true y aún no se envió.
    /// </summary>
    Task<ServiceResult<ResultadoEmisionPorEntidad>> ReintentarAsync(int reciboId, bool enviarMail, CancellationToken ct = default);

    /// <summary>Edita el contenido (líneas/importe/detalle) de un recibo Pendiente (sin CAE). Rechaza si ya tiene CAE.</summary>
    Task<ServiceResult<bool>> EditarReciboPendienteAsync(int reciboId, IReadOnlyList<ReciboLineaInput> lineas, CancellationToken ct = default);

    /// <summary>Elimina un recibo Pendiente (sin CAE) — p. ej. para rehacer uno trabado por una emisión fallida. Rechaza si ya tiene CAE.</summary>
    Task<ServiceResult<bool>> EliminarReciboPendienteAsync(int reciboId, CancellationToken ct = default);

    /// <summary>
    /// Anula un recibo emitido generando una nota de crédito. Si <paramref name="enviarMail"/> es true
    /// además envía la NC por mail; el fallo del mail NO falla la operación y se informa en
    /// <see cref="ResultadoAnulacion.ErrorMail"/>.
    /// </summary>
    Task<ServiceResult<ResultadoAnulacion>> AnularReciboAsync(int reciboId, bool enviarMail, CancellationToken ct = default);

    /// <summary>Reenvía el mail del comprobante: el recibo, o su nota de crédito si está Anulado (sin tocar estado).</summary>
    Task<ServiceResult<bool>> ReenviarMailAsync(int reciboId, CancellationToken ct = default);
    Task<ServiceResult<bool>> MarcarPagadoAsync(int reciboId, CancellationToken ct = default);

    /// <summary>Marca un recibo emitido e impago como incobrable (baja de la deuda), con motivo opcional.</summary>
    Task<ServiceResult<bool>> MarcarIncobrableAsync(int reciboId, string? motivo, CancellationToken ct = default);

    /// <summary>Revierte la baja por incobrable: el recibo vuelve a estar pendiente de cobro.</summary>
    Task<ServiceResult<bool>> QuitarIncobrableAsync(int reciboId, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<Recibo>>> GetPendientesAsync(FiltroPendientes filtro, CancellationToken ct = default);

    /// <summary>Una página de recibos para la sección "Control" (paginado server-side).</summary>
    Task<ServiceResult<PaginaResultado<Recibo>>> GetControlPaginadoAsync(FiltroControlPagos filtro, CancellationToken ct = default);
}
