using PesajeFaeneo.Domain.Entities;

namespace PesajeFaeneo.Application.Abstracciones.Persistencia;

public interface IClienteRepositorio
{
    Task<Cliente?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Cliente?> ObtenerPorCodigoAsync(int codigo, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Cliente>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default);
    Task<bool> TienePesajesAsync(int clienteId, CancellationToken cancellationToken = default);
    Task<int?> ObtenerCodigoMaximoAsync(CancellationToken cancellationToken = default);
    void Agregar(Cliente cliente);
}
