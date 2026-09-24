namespace PesajeFaeneo.Application.Abstracciones;

public interface IFormFactory
{
    T Crear<T>() where T : class;
}
