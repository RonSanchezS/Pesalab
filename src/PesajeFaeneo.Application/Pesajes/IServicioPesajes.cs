using PesajeFaeneo.Application.Pesajes.Dtos;

namespace PesajeFaeneo.Application.Pesajes;

public interface IServicioPesajes
{
    Task<SugerenciaSecuencia> SugerirSecuenciaAsync(int ganaderoId, CancellationToken cancellationToken = default);
    Task<PesajeRegistrado> RegistrarAsync(RegistrarPesaje dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PesajeResumen>> ListarDelDiaAsync(CancellationToken cancellationToken = default);

    /// <summary>Pesajes (vigentes y anulados) del día local indicado, el más reciente primero; tope <c>ReglasPesaje.HistorialMaximo</c>.</summary>
    Task<IReadOnlyList<PesajeResumen>> ListarPorDiaAsync(DateOnly dia, CancellationToken cancellationToken = default);
}
