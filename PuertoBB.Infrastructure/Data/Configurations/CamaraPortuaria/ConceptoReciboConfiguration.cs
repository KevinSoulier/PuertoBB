using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CamaraPortuaria;

namespace PuertoBB.Infrastructure.Data.Configurations.CamaraPortuaria;

public class ConceptoReciboConfiguration : IEntityTypeConfiguration<ConceptoRecibo>
{
    public void Configure(EntityTypeBuilder<ConceptoRecibo> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
        b.HasIndex(x => x.Nombre).IsUnique();
    }
}
