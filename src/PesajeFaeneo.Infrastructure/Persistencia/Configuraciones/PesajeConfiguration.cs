using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PesajeFaeneo.Domain.Entities;

namespace PesajeFaeneo.Infrastructure.Persistencia.Configuraciones;

public class PesajeConfiguration : IEntityTypeConfiguration<Pesaje>
{
    public void Configure(EntityTypeBuilder<Pesaje> builder)
    {
        builder.ToTable("Pesajes", t =>
        {
            t.HasCheckConstraint("CK_Pesajes_TipoDesposte", "TipoDesposte IN (1, 2)");
            t.HasCheckConstraint("CK_Pesajes_Sexo", "Sexo IN ('M', 'H')");
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.NumeroBoleta)
            .IsRequired();

        builder.HasIndex(p => p.NumeroBoleta)
            .IsUnique();

        builder.Property(p => p.FechaHora)
            .IsRequired();

        builder.HasIndex(p => p.FechaHora);

        builder.HasIndex(p => new { p.GanaderoId, p.FechaHora });
        builder.HasIndex(p => new { p.ClienteId, p.FechaHora });
        builder.HasIndex(p => new { p.Estado, p.FechaHora });

        builder.Property(p => p.Direccion)
            .HasMaxLength(150);

        builder.Property(p => p.Secuencia)
            .IsRequired();

        builder.Property(p => p.ColorRaza)
            .HasMaxLength(30);

        builder.Property(p => p.Sexo)
            .IsRequired();

        builder.Property(p => p.TipoDesposte)
            .IsRequired();

        var decimalToRealConverter = new ValueConverter<decimal, double>(
            v => (double)v,
            v => Math.Round((decimal)v, 1, MidpointRounding.AwayFromZero));

        builder.Property(p => p.PesoIzquierdo)
            .HasConversion(decimalToRealConverter)
            .HasColumnType("REAL")
            .IsRequired();

        builder.Property(p => p.PesoDerecho)
            .HasConversion(decimalToRealConverter)
            .HasColumnType("REAL")
            .IsRequired();

        builder.Property(p => p.PesoTotal)
            .HasConversion(decimalToRealConverter)
            .HasColumnType("REAL")
            .IsRequired();

        builder.Property(p => p.Observacion)
            .HasMaxLength(250);

        builder.Property(p => p.Estado)
            .IsRequired();

        builder.Property(p => p.MotivoAnulacion)
            .HasMaxLength(250);

        builder.Property(p => p.CreadoEn)
            .IsRequired();

        // Foreign keys with Restrict
        builder.HasOne(p => p.Ganadero)
            .WithMany(g => g.Pesajes)
            .HasForeignKey(p => p.GanaderoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Cliente)
            .WithMany(c => c.Pesajes)
            .HasForeignKey(p => p.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Usuario)
            .WithMany()
            .HasForeignKey(p => p.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.AnuladoPor)
            .WithMany()
            .HasForeignKey(p => p.AnuladoPorId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
