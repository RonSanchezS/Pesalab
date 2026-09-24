using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Infrastructure.Common;
using PesajeFaeneo.Infrastructure.Persistencia;
using PesajeFaeneo.Infrastructure.Seguridad;

namespace PesajeFaeneo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        var appPaths = new AppPaths();
        appPaths.AsegurarDirectorios();

        services.AddSingleton(appPaths);

        services.AddDbContextFactory<PesajeDbContext>(options =>
        {
            options.UseSqlite(appPaths.ConnectionString);
        });

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        services.AddScoped<IDataSeeder, DataSeeder>();

        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
