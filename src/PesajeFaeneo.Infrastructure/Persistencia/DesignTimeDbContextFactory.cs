using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PesajeFaeneo.Infrastructure.Common;

namespace PesajeFaeneo.Infrastructure.Persistencia;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PesajeDbContext>
{
    public PesajeDbContext CreateDbContext(string[] args)
    {
        var appPaths = new AppPaths();
        var optionsBuilder = new DbContextOptionsBuilder<PesajeDbContext>();
        optionsBuilder.UseSqlite(appPaths.ConnectionString);

        return new PesajeDbContext(optionsBuilder.Options);
    }
}
