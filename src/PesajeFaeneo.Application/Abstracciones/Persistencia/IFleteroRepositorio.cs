using PesajeFaeneo.Domain.Entities;

namespace PesajeFaeneo.Application.Abstracciones.Persistencia;

public interface IFleteroRepositorio
{
    Task<Fletero?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Fletero?> ObtenerActivoPorOrdenAsync(int orden, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Fletero>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default);
    Task<int> ContarActivosAsync(CancellationToken cancellationToken = default);
    void Agregar(Fletero fletero);
}
