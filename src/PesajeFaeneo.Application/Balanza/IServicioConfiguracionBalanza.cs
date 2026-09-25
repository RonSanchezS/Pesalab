using PesajeFaeneo.Application.Balanza.Dtos;

namespace PesajeFaeneo.Application.Balanza;

public interface IServicioConfiguracionBalanza
{
    Task<ConfiguracionBalanzaLeida> ObtenerAsync(CancellationToken cancellationToken = default);
    Task<bool> ActualizarAsync(ConfiguracionBalanzaDto dto, CancellationToken cancellationToken = default);
    IReadOnlyList<string> ListarPuertosSerie();
}
