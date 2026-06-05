using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Infrastructure.Data.Configurations.CentroMaritimo;

public class AgenciaGrupoConfiguration : IEntityTypeConfiguration<AgenciaGrupo>
{
    public void Configure(EntityTypeBuilder<AgenciaGrupo> b)
    {
        b.HasKey(ag => ag.Id);
        b.HasIndex(ag => new { ag.AgenciaId, ag.GrupoFacturacionId }).IsUnique();
    }
}
