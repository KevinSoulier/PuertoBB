using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CamaraPortuaria;

namespace PuertoBB.Infrastructure.Data.Configurations.CamaraPortuaria;

public class EmailEmpresaConfiguration : IEntityTypeConfiguration<EmailEmpresa>
{
    public void Configure(EntityTypeBuilder<EmailEmpresa> b)
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Email).IsRequired().HasMaxLength(200);
    }
}
