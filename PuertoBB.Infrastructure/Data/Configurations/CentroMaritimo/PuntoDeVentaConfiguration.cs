using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Infrastructure.Data.Configurations.CentroMaritimo;

public class PuntoDeVentaConfiguration : IEntityTypeConfiguration<PuntoDeVenta>
{
    public void Configure(EntityTypeBuilder<PuntoDeVenta> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.Nombre).HasMaxLength(100).IsRequired();

        // Defensivo: el Numero de PV es parte de la PK AFIP del comprobante; no puede repetirse
        // dentro de una misma configuración (no rompe el caso de un único PV).
        b.HasIndex(p => new { p.ConfiguracionId, p.Numero }).IsUnique();

        b.HasOne<Configuracion>()
            .WithMany(c => c.PuntosDeVenta)
            .HasForeignKey(p => p.ConfiguracionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Punto de venta por defecto (producción), activo, ligado al singleton Id = 1.
        b.HasData(new PuntoDeVenta
        {
            Id = 1,
            ConfiguracionId = 1,
            Nombre = "Principal",
            Numero = 1,
            UsarHomologacion = false,
            Activo = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
