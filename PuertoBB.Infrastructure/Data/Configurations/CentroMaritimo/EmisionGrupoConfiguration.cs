using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Infrastructure.Data.Configurations.CentroMaritimo;

public class EmisionGrupoConfiguration : IEntityTypeConfiguration<EmisionGrupo>
{
    public void Configure(EntityTypeBuilder<EmisionGrupo> b)
    {
        b.ToTable("EmisionesGrupo");
        b.HasKey(e => e.Id);

        // Bloqueo de duplicados de emisión: una agencia no puede tener dos recibos del mismo grupo/período.
        b.HasIndex(e => new { e.GrupoFacturacionId, e.ClienteId, e.PeriodoAnio, e.PeriodoMes }).IsUnique();

        // Un recibo pertenece a lo sumo a una emisión de grupo (relación 1:0..1 desde Recibo).
        b.HasIndex(e => e.ReciboId).IsUnique();

        // Borrar el grupo limpia sus emisiones; los recibos (auditoría) quedan intactos.
        b.HasOne(e => e.Grupo)
            .WithMany(g => g.Emisiones)
            .HasForeignKey(e => e.GrupoFacturacionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(e => e.Recibo)
            .WithOne(r => r.EmisionGrupo)
            .HasForeignKey<EmisionGrupo>(e => e.ReciboId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
