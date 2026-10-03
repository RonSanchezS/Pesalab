using PesajeFaeneo.Application.PuestaEnMarcha.Dtos;

namespace PesajeFaeneo.Application.PuestaEnMarcha;

public interface IServicioPuestaEnMarcha
{
    Task<EstadoPuestaEnMarcha> ObtenerEstadoAsync(CancellationToken cancellationToken = default);

    /// <summary>Marca ConfiguracionInicialCompleta (D16). Lanza ValidacionException si faltan pasos obligatorios.</summary>
    Task MarcarCompletaAsync(bool confirmaSimulador, CancellationToken cancellationToken = default);
}
