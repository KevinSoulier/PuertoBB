using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Infrastructure.Data.Configurations.CentroMaritimo;

public class GrupoFacturacionLineaConfiguration : IEntityTypeConfiguration<GrupoFacturacionLinea>
{
    public void Configure(EntityTypeBuilder<GrupoFacturacionLinea> b)
    {
        b.HasKey(i => i.Id);
        b.Property(i => i.Descripcion).HasMaxLength(500).IsRequired();
        b.Property(i => i.Cantidad).HasColumnType("TEXT");
        b.Property(i => i.PrecioUnitario).HasColumnType("TEXT");
        b.Property(i => i.Importe).HasColumnType("TEXT");
        b.HasIndex(i => i.GrupoFacturacionId);

        b.HasOne(i => i.Grupo)
            .WithMany(g => g.Lineas)
            .HasForeignKey(i => i.GrupoFacturacionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
