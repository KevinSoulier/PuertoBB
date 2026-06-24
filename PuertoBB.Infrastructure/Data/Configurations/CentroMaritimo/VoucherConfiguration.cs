using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Infrastructure.Data.Configurations.CentroMaritimo;

public class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> b)
    {
        b.HasKey(v => v.Id);
        b.Property(v => v.Importe).HasColumnType("TEXT");
        b.HasIndex(v => v.Numero).IsUnique();

        b.HasOne(v => v.Cliente).WithMany().HasForeignKey(v => v.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(v => v.Barco).WithMany().HasForeignKey(v => v.BarcoId).OnDelete(DeleteBehavior.Restrict);
        // Al borrar la consolidación (p. ej. recibo Pendiente eliminado), los vouchers vuelven a "libres".
        b.HasOne(v => v.Consolidacion).WithMany(c => c.Vouchers).HasForeignKey(v => v.ConsolidacionId).OnDelete(DeleteBehavior.SetNull);
    }
}
