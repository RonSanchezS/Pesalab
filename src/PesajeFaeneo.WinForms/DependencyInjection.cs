using Microsoft.Extensions.DependencyInjection;
using PesajeFaeneo.WinForms.Infraestructura;

namespace PesajeFaeneo.WinForms;

public static class DependencyInjection
{
    public static IServiceCollection AddWinForms(this IServiceCollection services)
    {
        services.AddSingleton<IFormFactory, FormFactory>();
        services.AddSingleton<Impresion.IImpresorBoletas, Impresion.ImpresorBoletasGdi>();
        services.AddTransient<Impresion.IServicioImpresionBoletas, Impresion.ServicioImpresionBoletas>();

#if CAPTURAS_MANUAL
        // Solo build de capturas para los manuales: reloj con desplazamiento (para sembrar pesajes de días anteriores).
        // Va después de AddInfrastructure, así reemplaza a TimeProvider.System.
        services.AddSingleton<TimeProvider>(Capturas.RelojDemo.Instancia);
#endif

        // Los formularios NO se registran en el contenedor: IFormFactory.Crear<T> los instancia con
        // ActivatorUtilities dentro de un scope propio (ver FormFactory), así que resolverlos aquí
        // permitiría también resolverlos desde la raíz y volver a fugarlos.

        return services;
    }
}
