namespace PesajeFaeneo.Infrastructure.Balanza;

internal interface IPuertoSerie : IDisposable
{
    string Nombre { get; }

    void Abrir();

    /// <summary>
    /// Devuelve la cantidad de bytes leídos (&gt; 0) o lanza TimeoutException si no llegó nada
    /// en el intervalo de lectura. Tras Cerrar() lanza ObjectDisposedException.
    /// </summary>
    int Leer(byte[] buffer, int offset, int count);

    void Cerrar();
}
