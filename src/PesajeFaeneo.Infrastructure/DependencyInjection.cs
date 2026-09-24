using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Infrastructure.Common;
using PesajeFaeneo.Infrastructure.Persistencia;
using PesajeFaeneo.Infrastructure.Seguridad;

namespace PesajeFaeneo.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Recibe <paramref name="appPaths"/> ya construido (y con <c>AsegurarDirectorios()</c> ya invocado)
    /// en lugar de crear su propia instancia: el registro de servicios no debe hacer E/S, y antes había
    /// dos instancias de <see cref="AppPaths"/> distintas (una aquí, otra en Program) más una creación de
    /// carpetas reales en <c>C:\ProgramData\PesajeFaeneo</c> cada vez que un test de DI construía el
    /// contenedor.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        AppPaths appPaths)
    {
        services.AddSingleton(appPaths);

        services.AddDbContextFactory<PesajeDbContext>(options =>
        {
            options.ConfigurarSqlite(appPaths.ConnectionString);
        });

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        services.AddScoped<IDataSeeder, DataSeeder>();

        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
