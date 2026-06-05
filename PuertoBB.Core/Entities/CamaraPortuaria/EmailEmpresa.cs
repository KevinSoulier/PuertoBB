using PuertoBB.Core.Entities.Common;

namespace PuertoBB.Core.Entities.CamaraPortuaria;

public class EmailEmpresa : BaseEntity
{
    public int     EmpresaId { get; set; }
    public Empresa Empresa   { get; set; } = null!;
    public string  Email     { get; set; } = string.Empty;
    public bool    Activo    { get; set; } = true;
}
