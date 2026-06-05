using PuertoBB.Core.Entities.Common;

namespace PuertoBB.Core.Entities.CentroMaritimo;

/// <summary>Join N:M Agencia ↔ GrupoFacturacion. Índice único: (AgenciaId, GrupoFacturacionId).</summary>
public class AgenciaGrupo : BaseEntity
{
    public int              AgenciaId          { get; set; }
    public Agencia          Agencia            { get; set; } = null!;
    public int              GrupoFacturacionId { get; set; }
    public GrupoFacturacion Grupo              { get; set; } = null!;
}
