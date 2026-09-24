using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PesajeFaeneo.Domain.Entities;

namespace PesajeFaeneo.Infrastructure.Persistencia.Configuraciones;

public class AuditoriaConfiguration : IEntityTypeConfiguration<Auditoria>
{
    public void Configure(EntityTypeBuilder<Auditoria> builder)
    {
        builder.ToTable("Auditorias");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.FechaHora)
            .IsRequired();

        builder.HasIndex(a => a.FechaHora);

        builder.HasIndex(a => new { a.Entidad, a.EntidadId });

        builder.Property(a => a.Accion)
            .IsRequired();

        builder.Property(a => a.Entidad)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntidadId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.Motivo)
            .HasMaxLength(250);

        builder.HasOne(a => a.Usuario)
            .WithMany()
            .HasForeignKey(a => a.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
