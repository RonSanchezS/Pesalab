using Microsoft.Extensions.DependencyInjection;
using PesajeFaeneo.WinForms.Infraestructura;

namespace PesajeFaeneo.WinForms;

public static class DependencyInjection
{
    public static IServiceCollection AddWinForms(this IServiceCollection services)
    {
        services.AddSingleton<IFormFactory, FormFactory>();

        // Los formularios NO se registran en el contenedor: IFormFactory.Crear<T> los instancia con
        // ActivatorUtilities dentro de un scope propio (ver FormFactory), así que resolverlos aquí
        // permitiría también resolverlos desde la raíz y volver a fugarlos.

        return services;
    }
}
