using PesajeFaeneo.Application.Catalogos.Busqueda;
using PesajeFaeneo.Application.Catalogos.Dtos;

namespace PesajeFaeneo.Application.Catalogos;

public interface IServicioGanaderos
{
    Task<IReadOnlyList<GanaderoListado>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default);
    Task<GanaderoListado> ObtenerAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ElementoBusqueda>> ListarParaBusquedaAsync(CancellationToken cancellationToken = default);
    Task<int> CrearAsync(CrearGanadero dto, CancellationToken cancellationToken = default);
    Task EditarAsync(EditarGanadero dto, CancellationToken cancellationToken = default);
    Task CambiarEstadoAsync(int id, bool activo, CancellationToken cancellationToken = default);
}
