using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PesajeFaeneo.Domain.Entities;

namespace PesajeFaeneo.Infrastructure.Persistencia.Configuraciones;

public class FleteroConfiguration : IEntityTypeConfiguration<Fletero>
{
    public void Configure(EntityTypeBuilder<Fletero> builder)
    {
        builder.ToTable("Fleteros", t =>
        {
            // F10 (O5/O6): sin límite de fleteros; el código es correlativo y automático.
            t.HasCheckConstraint("CK_Fleteros_Codigo", "Codigo >= 1");
        });

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Codigo)
            .IsRequired();

        // Único entre todos (activos e inactivos): el código no se reutiliza.
        builder.HasIndex(f => f.Codigo)
            .IsUnique();

        builder.Property(f => f.Nombre)
            .IsRequired()
            .HasMaxLength(100)
            // NOCASE para que las búsquedas de F2-06 sean insensibles a mayúsculas/minúsculas.
            .UseCollation("NOCASE");

        builder.Property(f => f.Celular)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(f => f.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(f => f.CreadoEn)
            .IsRequired();
    }
}
