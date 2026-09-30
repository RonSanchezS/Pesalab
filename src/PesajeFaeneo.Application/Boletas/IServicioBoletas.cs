using PesajeFaeneo.Application.Boletas.Dtos;

namespace PesajeFaeneo.Application.Boletas;

public interface IServicioBoletas
{
    Task<DatosBoleta> ObtenerPorIdAsync(int pesajeId, CancellationToken cancellationToken = default);
    Task<DatosBoleta> ObtenerPorNumeroAsync(long numeroBoleta, CancellationToken cancellationToken = default);
    Task RegistrarImpresionAsync(RegistroImpresion registro, CancellationToken cancellationToken = default);
    Task<DatosBoleta> CrearMuestraAsync(CancellationToken cancellationToken = default);
}
