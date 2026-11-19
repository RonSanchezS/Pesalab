using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Application.Licencias;

/// <summary>Almacén local del estado de la licencia (registro de Windows en producción).</summary>
public interface IAlmacenLicenciaLocal
{
    /// <summary>Lee los valores crudos; nunca lanza por un valor ausente o ilegible (devuelve null en ese campo).</summary>
    DatosLicenciaLocal Leer();

    /// <summary>Guarda inicio (d), última vez visto (l) y su firma (s). Puede lanzar si el almacén no es escribible.</summary>
    void EscribirPrueba(long inicio, long ultimo, string firma);

    /// <summary>Guarda el token de activación (a).</summary>
    void EscribirActivacion(string token);

    /// <summary>Borra la activación local (una revocación en el servidor).</summary>
    void LimpiarActivacion();
}

public enum ResultadoActivacionServidor
{
    Ok,
    Invalida,
    Revocada,
    YaUsada
}

public enum EstadoClaveRemota
{
    Ninguna,
    Activada,
    Revocada
}

/// <summary>Estado del equipo en el servidor: clave y fecha de inicio de la prueba (epoch) si la conoce.</summary>
public sealed record EstadoRemotoLicencia(EstadoClaveRemota Clave, long? InicioPrueba);

/// <summary>Cliente del servidor de licencias. Cada operación ya lleva el producto de la aplicación.</summary>
public interface IServidorLicencias
{
    Task<ResultadoActivacionServidor> ActivarAsync(string clave, string hwid, CancellationToken cancellationToken = default);

    /// <summary>Registra la prueba del equipo (una sola vez en el servidor) y devuelve su fecha de inicio (epoch), o null.</summary>
    Task<long?> RegistrarPruebaAsync(string hwid, CancellationToken cancellationToken = default);

    Task<EstadoRemotoLicencia> ConsultarAsync(string hwid, CancellationToken cancellationToken = default);
}

/// <summary>Identificador estable del equipo (hwid, hexadecimal en minúsculas).</summary>
public interface IIdentificadorEquipo
{
    string Hwid { get; }
}

/// <summary>El servidor no respondió o respondió con error. El mensaje ya está redactado para el usuario.</summary>
public sealed class ServidorLicenciasException : DomainException
{
    public ServidorLicenciasException(string mensaje) : base(mensaje) { }

    public ServidorLicenciasException(string mensaje, Exception inner) : base(mensaje, inner) { }
}
