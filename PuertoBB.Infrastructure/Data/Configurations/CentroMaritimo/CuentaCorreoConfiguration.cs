using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Infrastructure.Data.Configurations.CentroMaritimo;

public class CuentaCorreoConfiguration : IEntityTypeConfiguration<CuentaCorreo>
{
    public void Configure(EntityTypeBuilder<CuentaCorreo> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.Nombre).HasMaxLength(100).IsRequired();

        b.HasOne<Configuracion>()
            .WithMany(c => c.CuentasCorreo)
            .HasForeignKey(c => c.ConfiguracionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cuenta de correo por defecto, activa, ligada al singleton Id = 1.
        b.HasData(new CuentaCorreo
        {
            Id = 1,
            ConfiguracionId = 1,
            Nombre = "Principal",
            Activo = true,
            SmtpPort = 587,
            Autenticacion = 1, // Básica
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
