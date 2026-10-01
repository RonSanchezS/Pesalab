using PesajeFaeneo.Application.Pesajes.Dtos;

namespace PesajeFaeneo.Application.Pesajes;

public interface IServicioGestionPesajes
{
    Task<OpcionesBusquedaBoletas> ObtenerOpcionesBusquedaAsync(CancellationToken cancellationToken = default);
    Task<ResultadoBusquedaBoletas> BuscarAsync(FiltroBoletas filtro, CancellationToken cancellationToken = default);
    Task<BoletaParaEdicion> ObtenerParaEdicionAsync(int pesajeId, CancellationToken cancellationToken = default);
    Task<ResultadoEdicion> EditarAsync(EditarPesaje dto, CancellationToken cancellationToken = default);
    Task AnularAsync(AnularPesaje dto, CancellationToken cancellationToken = default);
}
