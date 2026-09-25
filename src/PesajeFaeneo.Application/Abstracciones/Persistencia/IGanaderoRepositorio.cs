using PesajeFaeneo.Domain.Entities;

namespace PesajeFaeneo.Application.Abstracciones.Persistencia;

public interface IGanaderoRepositorio
{
    Task<Ganadero?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Ganadero?> ObtenerPorMarcaAsync(string marca, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Ganadero>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default);
    Task<bool> TienePesajesAsync(int ganaderoId, CancellationToken cancellationToken = default);
    void Agregar(Ganadero ganadero);
}
