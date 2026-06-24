using PuertoBB.Core.Entities.Common;

namespace PuertoBB.Core.Entities.CentroMaritimo;

/// <summary>
/// Relación entre un conjunto de Vouchers y el Recibo consolidado que se generó al cerrar un período
/// para una agencia. El Recibo es autocontenido (entidad de auditoría): no conoce a los vouchers —
/// esta entidad es la única que vincula vouchers + período + recibo, igual que <see cref="EmisionGrupo"/>
/// hace con los grupos. Al borrar el recibo Pendiente, esta fila cascadea y los vouchers quedan libres.
///
/// Índice único parcial (ClienteId, PeriodoAnio, PeriodoMes) WHERE Pendiente: un solo consolidado
/// "work in progress" (sin CAE) por agencia/período, pero permite COMPLEMENTARIOS (cada uno con su CAE)
/// cuando aparecen vouchers olvidados después de emitir. <see cref="Pendiente"/> está denormalizado
/// (lo mantiene el servicio en sync con el EstadoFiscal del recibo) porque SQLite exige columnas de la
/// misma tabla en el filtro del índice. ClienteId/PeriodoAnio/PeriodoMes están denormalizados y deben
/// coincidir con el Recibo (invariante garantizado por el servicio de cierre).
/// </summary>
public class Consolidacion : BaseEntity
{
    public int    ReciboId { get; set; }
    public Recibo Recibo   { get; set; } = null!;

    public int ClienteId   { get; set; }
    public int PeriodoAnio { get; set; }
    public int PeriodoMes  { get; set; }

    /// <summary>Espejo de "el recibo sigue Pendiente (sin CAE)": denormalizado para el índice único parcial.
    /// El servicio lo pone en false al obtener el CAE y al anular.</summary>
    public bool Pendiente { get; set; } = true;

    public ICollection<Voucher> Vouchers { get; set; } = [];
}
