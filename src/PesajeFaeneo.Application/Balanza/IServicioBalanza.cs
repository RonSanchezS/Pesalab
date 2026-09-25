using PesajeFaeneo.Application.Balanza.Dtos;

namespace PesajeFaeneo.Application.Balanza;

public interface IServicioBalanza
{
    /// <summary>
    /// Última instantánea publicada. Seguro desde cualquier hilo. Sin permisos.
    /// </summary>
    InstantaneaBalanza Instantanea { get; }

    /// <summary>
    /// Copia de las últimas tramas crudas (máx. TramasRecientesMaximo), ya formateadas con
    /// TramaVisible.Formatear; la más reciente al final. Sin permisos.
    /// </summary>
    IReadOnlyList<string> ObtenerTramasRecientes();

    /// <summary>
    /// Lee la configuración de la BD y lanza el bucle de conexión en segundo plano. NO espera a que
    /// conecte. Si ya está iniciado, no hace nada.
    /// </summary>
    Task IniciarAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Detiene el bucle, cierra el lector y publica Detenida. Si no está iniciado, no hace nada.
    /// </summary>
    Task DetenerAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// DetenerAsync + IniciarAsync (relee la configuración de la BD).
    /// </summary>
    Task ReiniciarAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Exige ConfigurarBalanza. Valida la configuración candidata (ValidacionException si no es válida),
    /// pausa el bucle compartido (estado EnDiagnostico), se conecta con la candidata durante `duracion`
    /// informando cada trama por `progreso`, y al final (siempre, también ante error o cancelación)
    /// reanuda el bucle compartido con la configuración GUARDADA si estaba iniciado.
    /// La cancelación NO lanza: devuelve Cancelada = true. Los errores de conexión NO lanzan:
    /// devuelven Conecto = false y Error.
    /// </summary>
    Task<ResultadoPruebaBalanza> ProbarAsync(
        ConfiguracionBalanzaDto candidata,
        TimeSpan duracion,
        IProgress<AvancePruebaBalanza>? progreso,
        CancellationToken cancellationToken = default);
}
