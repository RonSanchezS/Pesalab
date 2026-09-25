using PesajeFaeneo.Domain.Entities;

namespace PesajeFaeneo.Application.Abstracciones.Persistencia;

public interface IParametrosRepositorio
{
    Task<ParametrosSistema> ObtenerAsync(CancellationToken cancellationToken = default);
    Task<long?> ObtenerUltimoNumeroBoletaAsync(CancellationToken cancellationToken = default);
}
