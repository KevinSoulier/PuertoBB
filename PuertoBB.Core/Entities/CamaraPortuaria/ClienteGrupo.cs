using PuertoBB.Core.Entities.Common;

namespace PuertoBB.Core.Entities.CamaraPortuaria;

/// <summary>Join N:M Cliente ↔ GrupoFacturacion. Índice único: (ClienteId, GrupoFacturacionId).</summary>
public class ClienteGrupo : BaseEntity
{
    public int              ClienteId          { get; set; }
    public Cliente          Cliente            { get; set; } = null!;
    public int              GrupoFacturacionId { get; set; }
    public GrupoFacturacion Grupo              { get; set; } = null!;
}
