using Microsoft.Extensions.DependencyInjection;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.WinForms.Formularios;
using PesajeFaeneo.WinForms.Infraestructura;

namespace PesajeFaeneo.WinForms;

public static class DependencyInjection
{
    public static IServiceCollection AddWinForms(this IServiceCollection services)
    {
        services.AddSingleton<IFormFactory, FormFactory>();

        // Registro de formularios como Transient
        services.AddTransient<FrmPrincipal>();

        return services;
    }
}
