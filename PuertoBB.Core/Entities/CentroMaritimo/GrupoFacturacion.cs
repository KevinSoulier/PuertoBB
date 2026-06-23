using PuertoBB.Core.Entities.Common;

namespace PuertoBB.Core.Entities.CentroMaritimo;

public class GrupoFacturacion : BaseEntity
{
    public string  Nombre      { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    /// <summary>Total denormalizado: suma de los <see cref="GrupoFacturacionLinea.Importe"/> (se recalcula al guardar).</summary>
    public decimal Importe     { get; set; }
    public bool    Activo      { get; set; } = true;

    /// <summary>Ítems a facturar a cada miembro del grupo.</summary>
    public ICollection<GrupoFacturacionLinea> Lineas   { get; set; } = [];
    public ICollection<ClienteGrupo>          Clientes { get; set; } = [];
    /// <summary>Emisiones realizadas con este grupo (relación grupo + período + recibo).</summary>
    public ICollection<EmisionGrupo>          Emisiones { get; set; } = [];
}
