using PuertoBB.Core.Entities.Common;

namespace PuertoBB.Core.Entities.CamaraPortuaria;

/// <summary>Join N:M Empresa ↔ GrupoFacturacion. Índice único: (EmpresaId, GrupoFacturacionId).</summary>
public class EmpresaGrupo : BaseEntity
{
    public int              EmpresaId          { get; set; }
    public Empresa          Empresa            { get; set; } = null!;
    public int              GrupoFacturacionId { get; set; }
    public GrupoFacturacion Grupo              { get; set; } = null!;
}
