using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PesajeFaeneo.Infrastructure.Common;

namespace PesajeFaeneo.Infrastructure.Persistencia;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PesajeDbContext>
{
    public PesajeDbContext CreateDbContext(string[] args)
    {
        var appPaths = new AppPaths();
        appPaths.AsegurarDirectorios();

        var optionsBuilder = new DbContextOptionsBuilder<PesajeDbContext>();
        optionsBuilder.ConfigurarSqlite(appPaths.ConnectionString);

        return new PesajeDbContext(optionsBuilder.Options);
    }
}
