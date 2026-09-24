using Microsoft.Extensions.DependencyInjection;
using PesajeFaeneo.Application.Abstracciones;

namespace PesajeFaeneo.WinForms.Infraestructura;

public class FormFactory : IFormFactory
{
    private readonly IServiceProvider _serviceProvider;

    public FormFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public T Crear<T>() where T : class
    {
        return _serviceProvider.GetRequiredService<T>();
    }
}
