using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CamaraPortuaria;

namespace PuertoBB.Infrastructure.Data.Configurations.CamaraPortuaria;

public class ClienteGrupoConfiguration : IEntityTypeConfiguration<ClienteGrupo>
{
    public void Configure(EntityTypeBuilder<ClienteGrupo> b)
    {
        b.HasKey(eg => eg.Id);
        b.HasIndex(eg => new { eg.ClienteId, eg.GrupoFacturacionId }).IsUnique();
    }
}
