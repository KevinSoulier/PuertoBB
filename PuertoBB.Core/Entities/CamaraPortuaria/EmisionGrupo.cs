using PuertoBB.Core.Entities.Common;

namespace PuertoBB.Core.Entities.CamaraPortuaria;

/// <summary>
/// Relación entre un GrupoFacturacion y el Recibo que se generó al emitirlo para un período.
/// El Recibo es autocontenido (entidad de auditoría): no conoce al grupo; esta entidad es la
/// única que vincula grupo + período facturado + recibo. Al borrar el grupo, estas filas
/// cascadean y los recibos quedan intactos.
/// Índice único (GrupoFacturacionId, EmpresaId, PeriodoAnio, PeriodoMes): anti-duplicados de emisión.
/// EmpresaId/PeriodoAnio/PeriodoMes están denormalizados y deben coincidir con el Recibo
/// (invariante garantizado por el servicio de emisión).
/// </summary>
public class EmisionGrupo : BaseEntity
{
    public int              GrupoFacturacionId { get; set; }
    public GrupoFacturacion Grupo              { get; set; } = null!;
    public int              ReciboId           { get; set; }
    public Recibo           Recibo             { get; set; } = null!;

    public int EmpresaId   { get; set; }
    public int PeriodoAnio { get; set; }
    public int PeriodoMes  { get; set; }
}
