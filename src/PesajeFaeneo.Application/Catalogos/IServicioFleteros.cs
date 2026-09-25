using PesajeFaeneo.Application.Catalogos.Dtos;

namespace PesajeFaeneo.Application.Catalogos;

public interface IServicioFleteros
{
    Task<IReadOnlyList<FleteroListado>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default);
    Task<FleteroListado> ObtenerAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FleteroListado>> ListarActivosAsync(CancellationToken cancellationToken = default);
    Task<int> CrearAsync(CrearFletero dto, CancellationToken cancellationToken = default);
    Task EditarAsync(EditarFletero dto, CancellationToken cancellationToken = default);
    Task CambiarEstadoAsync(int id, bool activo, CancellationToken cancellationToken = default);
}
