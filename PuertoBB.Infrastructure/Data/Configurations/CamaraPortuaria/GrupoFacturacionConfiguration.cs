using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CamaraPortuaria;

namespace PuertoBB.Infrastructure.Data.Configurations.CamaraPortuaria;

public class GrupoFacturacionConfiguration : IEntityTypeConfiguration<GrupoFacturacion>
{
    public void Configure(EntityTypeBuilder<GrupoFacturacion> b)
    {
        b.HasKey(g => g.Id);
        b.Property(g => g.Nombre).IsRequired().HasMaxLength(200);
        b.Property(g => g.Descripcion).HasMaxLength(500);
        b.Property(g => g.Importe).HasColumnType("TEXT"); // SQLite: decimal como TEXT para preservar precisión

        b.HasMany(g => g.Clientes).WithOne(x => x.Grupo).HasForeignKey(x => x.GrupoFacturacionId).OnDelete(DeleteBehavior.Cascade);
        // La relación con las emisiones (grupo + período + recibo) se configura en EmisionGrupoConfiguration.
    }
}
