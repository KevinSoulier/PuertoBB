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

        b.HasOne(v => v.Barco).WithMany().HasForeignKey(v => v.BarcoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(v => v.Recibo).WithMany(r => r.Vouchers).HasForeignKey(v => v.ReciboId).OnDelete(DeleteBehavior.Restrict);
    }
}
