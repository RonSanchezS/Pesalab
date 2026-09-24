using Microsoft.Extensions.DependencyInjection;

namespace PesajeFaeneo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<Seguridad.ISesionActual, Seguridad.SesionActual>();
        services.AddTransient<Seguridad.IServicioAutenticacion, Seguridad.ServicioAutenticacion>();
        services.AddTransient<Seguridad.IServicioUsuarios, Seguridad.ServicioUsuarios>();
        return services;
    }
}
