using System.Windows.Forms;
using PesajeFaeneo.WinForms.Infraestructura;

namespace PesajeFaeneo.Tests.Utilidades.Pesaje;

public class FormFactoryFalsa : IFormFactory
{
    private readonly Dictionary<Type, Func<Form>> _creadores = new();

    public void Registrar<T>(Func<T> creador) where T : Form
    {
        _creadores[typeof(T)] = creador;
    }

    public T Crear<T>() where T : Form
    {
        if (_creadores.TryGetValue(typeof(T), out var creador))
        {
            return (T)creador();
        }

        throw new InvalidOperationException($"No se registró un creador de formulario para el tipo {typeof(T).Name}.");
    }
}
