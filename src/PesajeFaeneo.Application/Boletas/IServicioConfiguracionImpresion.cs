using PesajeFaeneo.Application.Boletas.Dtos;

namespace PesajeFaeneo.Application.Boletas;

public interface IServicioConfiguracionImpresion
{
    Task<ConfiguracionImpresionEquipo> ObtenerAsync(CancellationToken cancellationToken = default);
    Task GuardarAsync(ConfiguracionImpresion configuracion, CancellationToken cancellationToken = default);
}
