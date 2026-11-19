namespace PesajeFaeneo.Application.Licencias;

/// <summary>
/// F13: clave de activación y prueba de 15 días. No exige sesión: se consulta antes del inicio de sesión.
/// </summary>
public interface IServicioLicencia
{
    /// <summary>Calcula el estado con el reloj actual (puede refrescar la última vez visto en el almacén).</summary>
    EstadoLicencia ObtenerEstado();

    /// <summary>True con licencia activada o prueba vigente.</summary>
    bool PermiteOperar { get; }

    /// <summary>D4: lanza <c>LIC_MODO_CONSULTA</c> si el sistema está en modo consulta. Va antes de cualquier transacción.</summary>
    void ExigirOperacion();

    /// <summary>Primeros 8 caracteres del identificador del equipo, en mayúsculas y con formato XXXX-XXXX.</summary>
    string CodigoEquipo { get; }

    Task<EstadoLicencia> ActivarAsync(string clave, CancellationToken cancellationToken = default);

    Task<EstadoLicencia> IniciarPruebaAsync(CancellationToken cancellationToken = default);

    /// <summary>D7: reconcilia con el servidor. Los errores de red no se propagan: devuelve el estado local.</summary>
    Task<EstadoLicencia> SincronizarAsync(CancellationToken cancellationToken = default);

    /// <summary>Se dispara (posiblemente desde otro hilo) cuando el estado calculado cambia.</summary>
    event EventHandler<EstadoLicencia>? EstadoCambiado;
}
