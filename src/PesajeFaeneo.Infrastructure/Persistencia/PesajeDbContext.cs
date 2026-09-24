using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Domain.Entities;

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
}
