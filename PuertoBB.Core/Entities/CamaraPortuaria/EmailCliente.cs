using PuertoBB.Core.Entities.Common;

namespace PuertoBB.Core.Entities.CamaraPortuaria;

public class EmailCliente : BaseEntity
{
    public int     ClienteId { get; set; }
    public Cliente Cliente   { get; set; } = null!;
    public string  Email     { get; set; } = string.Empty;
    public bool    Activo    { get; set; } = true;
}
