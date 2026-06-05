using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CamaraPortuaria;

namespace PuertoBB.Infrastructure.Data.Configurations.CamaraPortuaria;

public class ReciboConfiguration : IEntityTypeConfiguration<Recibo>
{
    public void Configure(EntityTypeBuilder<Recibo> b)
    {
        b.HasKey(r => r.Id);
        b.Property(r => r.Importe).HasColumnType("TEXT");
        b.Property(r => r.Detalle).HasMaxLength(1000);
        b.Property(r => r.CAE).HasMaxLength(20);
        b.Property(r => r.Estado).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.TipoComprobante).HasConversion<string>().HasMaxLength(20);

        // Bloqueo de duplicados: una entidad no puede tener dos recibos del mismo grupo/período.
        b.HasIndex(r => new { r.EmpresaId, r.GrupoFacturacionId, r.PeriodoAnio, r.PeriodoMes }).IsUnique();

        b.HasOne(r => r.NotaDeCredito)
            .WithOne(n => n.ReciboOriginal)
            .HasForeignKey<NotaDeCredito>(n => n.ReciboOriginalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
