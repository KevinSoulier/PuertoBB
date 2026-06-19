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
        b.Property(r => r.Detalle).HasMaxLength(2000); // unificado con CM (F-02)
        b.Property(r => r.CAE).HasMaxLength(20);
        b.Property(r => r.EstadoFiscal).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.TipoComprobante).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.MotivoIncobrable).HasMaxLength(300);

        // Snapshot fiscal del receptor (copiado al emitir)
        b.Property(r => r.ReceptorNombre).IsRequired().HasMaxLength(200);
        b.Property(r => r.ReceptorRazonSocial).IsRequired().HasMaxLength(200);
        b.Property(r => r.ReceptorCuit).IsRequired().HasMaxLength(13);
        b.Property(r => r.ReceptorDomicilio).HasMaxLength(300);
        b.Property(r => r.ReceptorCondicionIva).HasMaxLength(100);

        // El anti-duplicados de emisión por grupo vive en el índice único de EmisionesGrupo.

        b.HasIndex(r => new { r.PuntoDeVenta, r.NumeroComprobante, r.CodigoAfip })
            .IsUnique()
            .HasFilter("\"NumeroComprobante\" > 0");

        b.HasOne(r => r.NotaDeCredito)
            .WithOne(n => n.ReciboOriginal)
            .HasForeignKey<NotaDeCredito>(n => n.ReciboOriginalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
