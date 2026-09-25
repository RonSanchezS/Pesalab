using PesajeFaeneo.Application.Catalogos.Busqueda;
using PesajeFaeneo.Application.Catalogos.Dtos;

namespace PesajeFaeneo.Application.Catalogos;

public interface IServicioClientes
{
    Task<IReadOnlyList<ClienteListado>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default);
    Task<ClienteListado> ObtenerAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ElementoBusqueda>> ListarParaBusquedaAsync(CancellationToken cancellationToken = default);
    Task<int> CrearAsync(CrearCliente dto, CancellationToken cancellationToken = default);
    Task EditarAsync(EditarCliente dto, CancellationToken cancellationToken = default);
    Task CambiarEstadoAsync(int id, bool activo, CancellationToken cancellationToken = default);
    Task<int> SugerirSiguienteCodigoAsync(CancellationToken cancellationToken = default);
}
