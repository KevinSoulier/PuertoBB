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
        // El anti-duplicados de consolidado Pendiente vive en el índice único parcial de Consolidaciones.

        b.HasIndex(r => new { r.PuntoDeVenta, r.NumeroComprobante, r.CodigoAfip })
            .IsUnique()
            .HasFilter("\"NumeroComprobante\" > 0");

        // Índice para la sección "Control" (orden por período del paginado server-side).
        b.HasIndex(r => new { r.PeriodoAnio, r.PeriodoMes });

        b.HasOne(r => r.NotaDeCredito)
            .WithOne(n => n.ReciboOriginal)
            .HasForeignKey<NotaDeCredito>(n => n.ReciboOriginalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
