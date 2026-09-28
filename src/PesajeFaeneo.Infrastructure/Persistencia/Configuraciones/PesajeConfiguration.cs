using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Infrastructure.Persistencia.Conversores;

namespace PesajeFaeneo.Infrastructure.Persistencia.Configuraciones;

public class PesajeConfiguration : IEntityTypeConfiguration<Pesaje>
{
    public void Configure(EntityTypeBuilder<Pesaje> builder)
    {
        builder.ToTable("Pesajes", t =>
        {
            t.HasCheckConstraint("CK_Pesajes_TipoDesposte", "TipoDesposte IN (1, 2)");
            t.HasCheckConstraint("CK_Pesajes_Sexo", "Sexo IN ('M', 'H')");
            t.HasCheckConstraint("CK_Pesajes_Estado", "Estado IN (1, 2)");
            t.HasCheckConstraint(
                "CK_Pesajes_Pesos",
                "PesoIzquierdo >= 0 AND PesoDerecho >= 0 AND PesoTotal >= 0");
            t.HasCheckConstraint(
                "CK_Pesajes_Secuencia",
                "Secuencia BETWEEN 1 AND 9999");
            t.HasCheckConstraint(
                "CK_Pesajes_Anulacion",
                "(Estado = 1 AND MotivoAnulacion IS NULL AND AnuladoPorId IS NULL AND AnuladoEn IS NULL) " +
                "OR (Estado = 2 AND MotivoAnulacion IS NOT NULL AND AnuladoPorId IS NOT NULL AND AnuladoEn IS NOT NULL)");
            t.HasCheckConstraint("CK_Pesajes_NumeroBoleta", "NumeroBoleta >= 1");
            t.HasCheckConstraint("CK_Pesajes_PesoTotal", "abs(PesoTotal - (PesoIzquierdo + PesoDerecho)) < 0.05");
            t.HasCheckConstraint("CK_Pesajes_OrigenPeso", "OrigenPeso IN (1, 2, 3)");
            t.HasCheckConstraint("CK_Pesajes_Direccion", "length(Direccion) <= 150");
            t.HasCheckConstraint("CK_Pesajes_ColorRaza", "length(ColorRaza) <= 30");
            t.HasCheckConstraint("CK_Pesajes_Observacion", "length(Observacion) <= 250");
            t.HasCheckConstraint("CK_Pesajes_MotivoAnulacion", "MotivoAnulacion IS NULL OR length(MotivoAnulacion) BETWEEN 1 AND 250");
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

        builder.Property(p => p.PesoIzquierdo)
            .HasConversion(DecimalARealConverter.Instancia)
            .HasColumnType("REAL")
            .IsRequired();

        builder.Property(p => p.PesoDerecho)
            .HasConversion(DecimalARealConverter.Instancia)
            .HasColumnType("REAL")
            .IsRequired();

        builder.Property(p => p.PesoTotal)
            .HasConversion(DecimalARealConverter.Instancia)
            .HasColumnType("REAL")
            .IsRequired();

        builder.Property(p => p.OrigenPeso)
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
