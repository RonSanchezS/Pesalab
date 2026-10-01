using Microsoft.Extensions.DependencyInjection;

namespace PesajeFaeneo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<Seguridad.ISesionActual, Seguridad.SesionActual>();
        services.AddTransient<Seguridad.IServicioAutenticacion, Seguridad.ServicioAutenticacion>();
        services.AddTransient<Seguridad.IServicioUsuarios, Seguridad.ServicioUsuarios>();
        services.AddTransient<Catalogos.IServicioGanaderos, Catalogos.ServicioGanaderos>();
        services.AddTransient<Catalogos.IServicioClientes, Catalogos.ServicioClientes>();
        services.AddTransient<Catalogos.IServicioFleteros, Catalogos.ServicioFleteros>();
        services.AddTransient<Parametros.IServicioParametros, Parametros.ServicioParametros>();
        services.AddSingleton<Balanza.IServicioBalanza, Balanza.ServicioBalanza>();
        services.AddTransient<Balanza.IServicioConfiguracionBalanza, Balanza.ServicioConfiguracionBalanza>();
        services.AddTransient<Pesajes.IServicioPesajes, Pesajes.ServicioPesajes>();
        services.AddTransient<Boletas.IServicioBoletas, Boletas.ServicioBoletas>();
        services.AddTransient<Boletas.IServicioConfiguracionImpresion, Boletas.ServicioConfiguracionImpresion>();
        services.AddTransient<Pesajes.IServicioGestionPesajes, Pesajes.ServicioGestionPesajes>();
        services.AddTransient<Auditoria.IServicioAuditoria, Auditoria.ServicioAuditoria>();
        return services;
    }
}
