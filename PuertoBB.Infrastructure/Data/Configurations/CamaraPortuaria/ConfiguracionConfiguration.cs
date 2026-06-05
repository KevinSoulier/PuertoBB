using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CamaraPortuaria;

namespace PuertoBB.Infrastructure.Data.Configurations.CamaraPortuaria;

public class ConfiguracionConfiguration : IEntityTypeConfiguration<Configuracion>
{
    public void Configure(EntityTypeBuilder<Configuracion> b)
    {
        b.HasKey(c => c.Id);

        // Singleton Id = 1 sembrado de fábrica.
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
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
