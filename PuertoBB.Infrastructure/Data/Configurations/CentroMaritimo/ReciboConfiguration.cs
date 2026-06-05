using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Infrastructure.Data.Configurations.CentroMaritimo;

public class ReciboConfiguration : IEntityTypeConfiguration<Recibo>
{
    public void Configure(EntityTypeBuilder<Recibo> b)
    {
        b.HasKey(r => r.Id);
        b.Property(r => r.Importe).HasColumnType("TEXT");
        b.Property(r => r.Detalle).HasMaxLength(2000);
        b.Property(r => r.CAE).HasMaxLength(20);
        b.Property(r => r.NombreApoderado).HasMaxLength(200);
        b.Property(r => r.CuitApoderado).HasMaxLength(13);
        b.Property(r => r.Estado).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.TipoComprobante).HasConversion<string>().HasMaxLength(20);

        // Bloqueo de duplicados de cuota/individual.
        b.HasIndex(r => new { r.AgenciaId, r.GrupoFacturacionId, r.PeriodoAnio, r.PeriodoMes }).IsUnique();

        // Un solo recibo consolidado de vouchers por (agencia, período) — índice único parcial.
        b.HasIndex(r => new { r.AgenciaId, r.PeriodoAnio, r.PeriodoMes })
            .IsUnique()
            .HasFilter("\"EsConsolidadoVouchers\" = 1");

        b.HasOne(r => r.NotaDeCredito)
            .WithOne(n => n.ReciboOriginal)
            .HasForeignKey<NotaDeCredito>(n => n.ReciboOriginalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
