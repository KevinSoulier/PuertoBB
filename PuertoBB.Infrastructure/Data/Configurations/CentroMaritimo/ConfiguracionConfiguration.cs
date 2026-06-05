using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Infrastructure.Data.Configurations.CentroMaritimo;

public class ConfiguracionConfiguration : IEntityTypeConfiguration<Configuracion>
{
    public void Configure(EntityTypeBuilder<Configuracion> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.NombreApoderado).HasMaxLength(200);
        b.Property(c => c.CuitApoderado).HasMaxLength(13);

        b.HasData(new Configuracion
        {
            Id = 1,
            RazonSocial = string.Empty,
            Cuit = string.Empty,
            PuntoDeVenta = 1,
            CodigoAfipRecibo = 11,
            CodigoAfipNotaDeCredito = 13,
            DiasVencimiento = 30,
            SmtpPort = 587,
            UsarApoderado = false,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
