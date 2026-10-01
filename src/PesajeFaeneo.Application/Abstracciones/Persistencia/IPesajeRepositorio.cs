using PesajeFaeneo.Domain.Entities;

namespace PesajeFaeneo.Application.Abstracciones.Persistencia;

public interface IPesajeRepositorio
{
    void Agregar(Pesaje pesaje);

    Task<Pesaje?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// MAX(Secuencia) de pesajes VIGENTES del ganadero con FechaHora en [desdeUtc, hastaUtc); null si no hay.
    /// </summary>
    Task<int?> ObtenerSecuenciaMaximaAsync(int ganaderoId, DateTime desdeUtc, DateTime hastaUtc, CancellationToken cancellationToken = default);

    Task<int> ContarVigentesAsync(int ganaderoId, DateTime desdeUtc, DateTime hastaUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sin tracking, Include Ganadero y Cliente, orden NumeroBoleta descendente, Take(maximo).
    /// </summary>
    Task<IReadOnlyList<Pesaje>> ListarPorRangoAsync(DateTime desdeUtc, DateTime hastaUtc, int maximo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sin tracking; Include Ganadero, Cliente y Usuario. null si no existe.
    /// </summary>
    Task<Pesaje?> ObtenerParaBoletaPorIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sin tracking; Include Ganadero, Cliente y Usuario. null si no existe.
    /// </summary>
    Task<Pesaje?> ObtenerParaBoletaPorNumeroAsync(long numeroBoleta, CancellationToken cancellationToken = default);

    /// <summary>
    /// CON tracking (los cambios se guardan con GuardarCambiosAsync); Include Ganadero y Cliente; null si no existe.
    /// </summary>
    Task<Pesaje?> ObtenerParaEdicionAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sin tracking; Include Ganadero y Cliente; orden NumeroBoleta descendente; Take(maximo).
    /// </summary>
    Task<IReadOnlyList<Pesaje>> BuscarAsync(CriteriosBusquedaPesaje criterios, int maximo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sin tracking; Include Ganadero, Cliente y AnuladoPor; orden FechaHora y NumeroBoleta ascendentes; Take(maximo).
    /// </summary>
    Task<IReadOnlyList<Pesaje>> ListarParaReporteAsync(CriteriosReporte criterios, int maximo, CancellationToken cancellationToken = default);
}
