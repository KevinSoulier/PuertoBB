using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Infrastructure.Data.Configurations.CentroMaritimo;

public class EmailAgenciaConfiguration : IEntityTypeConfiguration<EmailAgencia>
{
    public void Configure(EntityTypeBuilder<EmailAgencia> b)
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Email).IsRequired().HasMaxLength(200);
    }
}
