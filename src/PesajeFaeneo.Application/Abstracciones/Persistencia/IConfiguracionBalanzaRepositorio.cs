using PesajeFaeneo.Domain.Entities;

namespace PesajeFaeneo.Application.Abstracciones.Persistencia;

public interface IConfiguracionBalanzaRepositorio
{
    /// <summary>
    /// Con tracking. La fila Id = 1 la siembra HasData; si faltara, lanza InvalidOperationException.
    /// </summary>
    Task<ConfiguracionBalanza> ObtenerAsync(CancellationToken cancellationToken = default);
}
