using PuertoBB.Core.Entities.Common;

namespace PuertoBB.Core.Entities.CentroMaritimo;

public class EmailAgencia : BaseEntity
{
    public int     AgenciaId { get; set; }
    public Agencia Agencia   { get; set; } = null!;
    public string  Email     { get; set; } = string.Empty;
    public bool    Activo    { get; set; } = true;
}
