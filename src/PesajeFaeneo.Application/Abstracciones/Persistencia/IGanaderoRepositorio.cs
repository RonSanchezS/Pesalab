using PesajeFaeneo.Domain.Entities;

namespace PesajeFaeneo.Application.Abstracciones.Persistencia;

public interface IGanaderoRepositorio
{
    Task<Ganadero?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Ganadero?> ObtenerPorCodigoAsync(string codigo, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Ganadero>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default);
    Task<bool> TienePesajesAsync(int ganaderoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mayor código numérico entre TODOS los ganaderos (activos e inactivos); los códigos alfanuméricos heredados del
    /// sistema legado se ignoran. 0 si no hay ninguno numérico (F10, O6).
    /// </summary>
    Task<int> ObtenerCodigoNumericoMaximoAsync(CancellationToken cancellationToken = default);
    void Agregar(Ganadero ganadero);
}
