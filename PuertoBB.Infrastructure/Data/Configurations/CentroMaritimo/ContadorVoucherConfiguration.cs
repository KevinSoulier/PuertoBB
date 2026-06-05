using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Infrastructure.Data.Configurations.CentroMaritimo;

public class ContadorVoucherConfiguration : IEntityTypeConfiguration<ContadorVoucher>
{
    public void Configure(EntityTypeBuilder<ContadorVoucher> b)
    {
        b.HasKey(c => c.Id);

        // Singleton Id = 1; UltimoNumero editable para fijar el inicial al migrar del sistema manual.
        b.HasData(new ContadorVoucher
        {
            Id = 1,
            UltimoNumero = 0,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
