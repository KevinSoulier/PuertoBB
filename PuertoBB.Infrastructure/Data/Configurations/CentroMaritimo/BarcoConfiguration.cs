using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Infrastructure.Data.Configurations.CentroMaritimo;

public class BarcoConfiguration : IEntityTypeConfiguration<Barco>
{
    public void Configure(EntityTypeBuilder<Barco> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
        b.HasIndex(x => x.Nombre).IsUnique();
    }
}
