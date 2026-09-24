using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PesajeFaeneo.Domain.Entities;

namespace PesajeFaeneo.Infrastructure.Persistencia.Configuraciones;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuarios", t =>
        {
            t.HasCheckConstraint("CK_Usuarios_Rol", "Rol IN (1, 2, 3)");
            t.HasCheckConstraint("CK_Usuarios_IntentosFallidos", "IntentosFallidos >= 0");
        });

        builder.HasKey(u => u.Id);

        builder.Property(u => u.NombreUsuario)
            .IsRequired()
            .HasMaxLength(30)
            // NOCASE evita que "admin" y "Admin" coexistan como usuarios distintos: el login debe ser
            // insensible a mayúsculas/minúsculas.
            .UseCollation("NOCASE");

        builder.HasIndex(u => u.NombreUsuario)
            .IsUnique();

        builder.Property(u => u.NombreCompleto)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.Hash)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.Salt)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(u => u.Rol)
            .IsRequired();

        builder.Property(u => u.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(u => u.DebeCambiarPassword)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(u => u.IntentosFallidos)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(u => u.BloqueadoHasta)
            .IsRequired(false);

        builder.Property(u => u.CreadoEn)
            .IsRequired();
    }
}
