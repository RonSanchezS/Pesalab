using PesajeFaeneo.Application.Seguridad.Dtos;

namespace PesajeFaeneo.Application.Seguridad;

public interface IServicioUsuarios
{
    Task<IReadOnlyList<UsuarioListado>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default);
    Task<UsuarioListado> ObtenerAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CrearAsync(CrearUsuario dto, CancellationToken cancellationToken = default);
    Task EditarAsync(EditarUsuario dto, CancellationToken cancellationToken = default);
    Task CambiarEstadoAsync(int id, bool activo, CancellationToken cancellationToken = default);
    Task RestablecerPasswordAsync(int id, string nueva, string confirmacion, CancellationToken cancellationToken = default);
}
