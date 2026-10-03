namespace PesajeFaeneo.WinForms.Infraestructura;

/// <summary>
/// Mutex de instancia única. Tras una restauración, la aplicación se relanza con --reinicio y la nueva
/// instancia espera a que la anterior termine en lugar de avisar "ya está en ejecución".
/// Un Mutex pertenece al hilo que lo adquirió: crear y desechar en el mismo hilo.
/// </summary>
public sealed class InstanciaUnica : IDisposable
{
    public const string ArgumentoReinicio = "--reinicio";

    private readonly Mutex _mutex;
    private bool _liberada;

    private InstanciaUnica(Mutex mutex) => _mutex = mutex;

    public static InstanciaUnica? Adquirir(string nombre, bool esperar, TimeSpan espera)
        => Adquirir(nombre, esperar, espera, n =>
        {
            var m = new Mutex(true, n, out var esNueva);
            return (m, esNueva);
        });

    internal static InstanciaUnica? Adquirir(
        string nombre, bool esperar, TimeSpan espera, Func<string, (Mutex Mutex, bool EsNueva)> crear)
    {
        Mutex mutex;
        bool esNueva;
        try
        {
            (mutex, esNueva) = crear(nombre);
        }
        catch (UnauthorizedAccessException)
        {
            // El mutex Global\ lo creó otro usuario de Windows en otra sesión (H13): el sistema ya está abierto en este equipo.
            return null;
        }

        if (esNueva)
        {
            return new InstanciaUnica(mutex);
        }

        if (esperar)
        {
            try
            {
                if (mutex.WaitOne(espera))
                {
                    return new InstanciaUnica(mutex);
                }
            }
            catch (AbandonedMutexException)
            {
                // La instancia anterior terminó sin liberarlo: la propiedad pasa a este hilo (H8).
                return new InstanciaUnica(mutex);
            }
        }

        mutex.Dispose();
        return null;
    }

    public void Dispose()
    {
        if (_liberada)
        {
            return;
        }

        _liberada = true;
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // No pertenecía a este hilo: no hay nada que liberar.
        }

        _mutex.Dispose();
    }
}
