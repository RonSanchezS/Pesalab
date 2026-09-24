using PesajeFaeneo.Domain.Entities;

namespace PesajeFaeneo.Application.Abstracciones.Persistencia;

public interface IUsuarioRepositorio
{
    Task<Usuario?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);            // con tracking
    Task<Usuario?> ObtenerPorNombreUsuarioAsync(string nombreUsuario, CancellationToken cancellationToken = default); // con tracking
    Task<IReadOnlyList<Usuario>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default);  // sin tracking, orden NombreUsuario
    Task<bool> ExisteNombreUsuarioAsync(string nombreUsuario, CancellationToken cancellationToken = default);
    Task<int> ContarAdministradoresActivosAsync(CancellationToken cancellationToken = default);
    void Agregar(Usuario usuario);
}
