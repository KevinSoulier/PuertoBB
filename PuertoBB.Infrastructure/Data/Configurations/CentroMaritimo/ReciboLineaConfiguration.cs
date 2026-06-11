using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Infrastructure.Data.Configurations.CentroMaritimo;

public class ReciboLineaConfiguration : IEntityTypeConfiguration<ReciboLinea>
{
    public void Configure(EntityTypeBuilder<ReciboLinea> b)
    {
        b.HasKey(i => i.Id);
        b.Property(i => i.Descripcion).HasMaxLength(500).IsRequired();
        b.Property(i => i.Cantidad).HasColumnType("TEXT");
        b.Property(i => i.PrecioUnitario).HasColumnType("TEXT");
        b.Property(i => i.Importe).HasColumnType("TEXT");
        b.HasIndex(i => i.ReciboId);

        b.HasOne(i => i.Recibo)
            .WithMany(r => r.Lineas)
            .HasForeignKey(i => i.ReciboId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
