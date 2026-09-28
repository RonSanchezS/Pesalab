using PesajeFaeneo.Application.Pesajes.Dtos;

namespace PesajeFaeneo.Application.Pesajes;

public interface IServicioPesajes
{
    Task<SugerenciaSecuencia> SugerirSecuenciaAsync(int ganaderoId, CancellationToken cancellationToken = default);
    Task<PesajeRegistrado> RegistrarAsync(RegistrarPesaje dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PesajeResumen>> ListarDelDiaAsync(CancellationToken cancellationToken = default);
}
