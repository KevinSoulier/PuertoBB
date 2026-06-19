using System.ComponentModel.DataAnnotations.Schema;
using PuertoBB.Core.Entities.Common;

namespace PuertoBB.Core.Entities.CamaraPortuaria;

public class Empresa : BaseEntity
{
    public string  Nombre        { get; set; } = string.Empty;
    public string  RazonSocial   { get; set; } = string.Empty;
    public string  Cuit          { get; set; } = string.Empty;
    public string? Domicilio     { get; set; }
    public int?    CondicionIvaId { get; set; } // código AFIP (CatalogoCondicionesIvaReceptor), ej. 1 = IVA Responsable Inscripto
    public bool    Activa        { get; set; } = true;

    public ICollection<EmailEmpresa> Emails  { get; set; } = [];
    public ICollection<EmpresaGrupo> Grupos  { get; set; } = [];
    public ICollection<Recibo>       Recibos { get; set; } = [];

    /// <summary>Emails concatenados para mostrar en grillas (no se persiste).</summary>
    [NotMapped]
    public string EmailsTexto => string.Join("; ", Emails.Select(e => e.Email));
}
