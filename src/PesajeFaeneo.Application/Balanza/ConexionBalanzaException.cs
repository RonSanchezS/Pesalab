namespace PesajeFaeneo.Application.Balanza;

/// <summary>
/// Error de comunicación con la balanza. Su Message siempre es apto para mostrarse al usuario.
/// </summary>
public sealed class ConexionBalanzaException : Exception
{
    public ConexionBalanzaException(string mensaje) : base(mensaje) { }
    public ConexionBalanzaException(string mensaje, Exception inner) : base(mensaje, inner) { }

    /// <summary>
    /// True cuando el estado EstadoBalanza.SinDatos ya fue publicado como causa de este error (D6);
    /// permite que el bucle de reconexión no lo sobrescriba de inmediato con EstadoBalanza.Error.
    /// </summary>
    public bool EsSinDatos { get; init; }
}
