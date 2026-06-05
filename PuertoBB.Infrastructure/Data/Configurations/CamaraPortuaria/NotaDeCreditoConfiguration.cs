using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CamaraPortuaria;

namespace PuertoBB.Infrastructure.Data.Configurations.CamaraPortuaria;

public class NotaDeCreditoConfiguration : IEntityTypeConfiguration<NotaDeCredito>
{
    public void Configure(EntityTypeBuilder<NotaDeCredito> b)
    {
        b.HasKey(n => n.Id);
        b.Property(n => n.CAE).HasMaxLength(20);
        b.Property(n => n.TipoComprobante).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(n => n.ReciboOriginalId).IsUnique();
    }
}
