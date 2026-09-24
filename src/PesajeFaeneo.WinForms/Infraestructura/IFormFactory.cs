namespace PesajeFaeneo.WinForms.Infraestructura;

/// <summary>
/// Fábrica de formularios. Vive en WinForms (no en Application) porque "formulario" es un concepto
/// de presentación; restringir a <see cref="Form"/> evita que se use como service locator genérico.
/// </summary>
public interface IFormFactory
{
    T Crear<T>() where T : Form;
}
