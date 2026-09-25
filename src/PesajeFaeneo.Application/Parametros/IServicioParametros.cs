using PesajeFaeneo.Application.Parametros.Dtos;

namespace PesajeFaeneo.Application.Parametros;

public interface IServicioParametros
{
    Task<ParametrosDto> ObtenerAsync(CancellationToken cancellationToken = default);
    Task ActualizarAsync(ActualizarParametros dto, CancellationToken cancellationToken = default);
}
