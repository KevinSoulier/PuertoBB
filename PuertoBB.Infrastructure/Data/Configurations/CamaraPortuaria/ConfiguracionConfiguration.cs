using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Mail;

namespace PuertoBB.Infrastructure.Data.Configurations.CamaraPortuaria;

public class ConfiguracionConfiguration : IEntityTypeConfiguration<Configuracion>
{
    public void Configure(EntityTypeBuilder<Configuracion> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.IngresosBrutos).HasMaxLength(50);

        // Singleton Id = 1 sembrado de fábrica.
        b.HasData(new Configuracion
        {
            Id = 1,
            RazonSocial = string.Empty,
            Cuit = string.Empty,
            CodigoAfipRecibo = 11,
            CodigoAfipNotaDeCredito = 13,
            DiasVencimiento = 15,
            MailAsunto = PlantillaMail.DefaultAsunto,
            MailCuerpo = PlantillaMail.DefaultCuerpoTexto,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
