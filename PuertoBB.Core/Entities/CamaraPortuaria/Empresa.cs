using PuertoBB.Core.Entities.Common;

namespace PuertoBB.Core.Entities.CamaraPortuaria;

public class Empresa : BaseEntity
{
    public string  Nombre      { get; set; } = string.Empty;
    public string  RazonSocial { get; set; } = string.Empty;
    public string  Cuit        { get; set; } = string.Empty;
    public string? Domicilio   { get; set; }
    public bool    Activa      { get; set; } = true;

    public ICollection<EmailEmpresa> Emails  { get; set; } = [];
    public ICollection<EmpresaGrupo> Grupos  { get; set; } = [];
    public ICollection<Recibo>       Recibos { get; set; } = [];
}
