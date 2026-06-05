using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CamaraPortuaria;

namespace PuertoBB.Infrastructure.Data.Configurations.CamaraPortuaria;

public class EmpresaGrupoConfiguration : IEntityTypeConfiguration<EmpresaGrupo>
{
    public void Configure(EntityTypeBuilder<EmpresaGrupo> b)
    {
        b.HasKey(eg => eg.Id);
        b.HasIndex(eg => new { eg.EmpresaId, eg.GrupoFacturacionId }).IsUnique();
    }
}
