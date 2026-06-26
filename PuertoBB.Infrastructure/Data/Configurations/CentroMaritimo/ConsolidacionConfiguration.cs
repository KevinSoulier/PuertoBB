using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Infrastructure.Data.Configurations.CentroMaritimo;

public class ConsolidacionConfiguration : IEntityTypeConfiguration<Consolidacion>
{
    public void Configure(EntityTypeBuilder<Consolidacion> b)
    {
        b.ToTable("Consolidaciones");
        b.HasKey(c => c.Id);

        // Un solo consolidado SIN CAE (Pendiente) por (agencia, período) — índice único parcial: evita dos
        // work-in-progress simultáneos, pero permite consolidados COMPLEMENTARIOS (cada uno con su CAE) cuando
        // aparecen vouchers olvidados después de emitir. 'Pendiente' está denormalizado (espejo del EstadoFiscal
        // del recibo) porque SQLite exige columnas de la misma tabla en el filtro del índice. Los recibos por
        // voucher (Individual = 1) quedan FUERA del índice: puede haber varios pendientes por agencia/período.
        b.HasIndex(c => new { c.ClienteId, c.PeriodoAnio, c.PeriodoMes })
            .IsUnique()
            .HasFilter("\"Pendiente\" = 1 AND \"Individual\" = 0");

        // Una consolidación pertenece a exactamente un recibo (1:1). El recibo no conoce la consolidación
        // (entidad de auditoría autocontenida); borrar el recibo Pendiente cascadea la consolidación y
        // libera sus vouchers (Voucher.ConsolidacionId → SetNull).
        b.HasIndex(c => c.ReciboId).IsUnique();
        b.HasOne(c => c.Recibo)
            .WithOne()
            .HasForeignKey<Consolidacion>(c => c.ReciboId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
