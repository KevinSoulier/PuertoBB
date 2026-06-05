using PuertoBB.Core.Entities.Common;

namespace PuertoBB.Core.Entities.CamaraPortuaria;

public class GrupoFacturacion : BaseEntity
{
    public string  Nombre      { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Importe     { get; set; }
    public bool    Activo      { get; set; } = true;

    public ICollection<EmpresaGrupo> Empresas { get; set; } = [];
    public ICollection<Recibo>       Recibos  { get; set; } = [];
}
