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

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        IncrementarVersionPesajes();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        IncrementarVersionPesajes();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // Token gestionado por la aplicación (H2): cada Pesaje modificado avanza su Version a partir del
    // valor ORIGINAL leído de BD (H3), de modo que reintentar SaveChanges no la incrementa dos veces.
    private void IncrementarVersionPesajes()
    {
        ChangeTracker.DetectChanges();
        foreach (var entrada in ChangeTracker.Entries<Pesaje>().Where(e => e.State == EntityState.Modified).ToList())
        {
            var version = entrada.Property(p => p.Version);
            version.CurrentValue = version.OriginalValue + 1;
        }
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
