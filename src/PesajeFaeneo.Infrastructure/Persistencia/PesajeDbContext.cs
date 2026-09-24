using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Infrastructure.Persistencia.Conversores;

namespace PesajeFaeneo.Infrastructure.Persistencia;

public class PesajeDbContext : DbContext
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<ParametrosSistema> ParametrosSistema => Set<ParametrosSistema>();
    public DbSet<ConfiguracionBalanza> ConfiguracionesBalanza => Set<ConfiguracionBalanza>();
    public DbSet<Ganadero> Ganaderos => Set<Ganadero>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Fletero> Fleteros => Set<Fletero>();
    public DbSet<Pesaje> Pesajes => Set<Pesaje>();
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();

    public PesajeDbContext(DbContextOptions<PesajeDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PesajeDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Configuración previa a las convenciones: se aplica a TODA propiedad DateTime/DateTime? del
        // modelo (ver DateTimeUtcConverter para el porqué) sin tener que repetirla en cada Configuration.
        configurationBuilder.Properties<DateTime>().HaveConversion<DateTimeUtcConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<DateTimeUtcNulableConverter>();
    }
}
