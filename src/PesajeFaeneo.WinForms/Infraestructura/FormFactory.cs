using Microsoft.Extensions.DependencyInjection;

namespace PesajeFaeneo.WinForms.Infraestructura;

/// <summary>
/// Crea formularios dentro de un <see cref="IServiceScope"/> propio en lugar de resolverlos del
/// contenedor raíz: MS.DI retiene para siempre todo <see cref="IDisposable"/> transient creado por
/// el proveedor raíz, así que cada pantalla abierta desde F1+ acumularía formularios y sus handles
/// GDI hasta el cierre de la aplicación. Al crear un scope por formulario y liberarlo cuando el
/// formulario se cierra, tanto el formulario como sus servicios scoped se desechan de inmediato.
/// </summary>
public class FormFactory : IFormFactory
{
    private readonly IServiceProvider _serviceProvider;

    public FormFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public T Crear<T>() where T : Form
    {
        var scope = _serviceProvider.CreateScope();
        var formulario = ActivatorUtilities.CreateInstance<T>(scope.ServiceProvider);
        formulario.Disposed += (_, _) => scope.Dispose();
        return formulario;
    }
}
