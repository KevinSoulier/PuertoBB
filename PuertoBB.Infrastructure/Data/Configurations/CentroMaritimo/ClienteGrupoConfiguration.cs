using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Infrastructure.Data.Configurations.CentroMaritimo;

public class ClienteGrupoConfiguration : IEntityTypeConfiguration<ClienteGrupo>
{
    public void Configure(EntityTypeBuilder<ClienteGrupo> b)
    {
        b.HasKey(ag => ag.Id);
        b.HasIndex(ag => new { ag.ClienteId, ag.GrupoFacturacionId }).IsUnique();
    }
}
