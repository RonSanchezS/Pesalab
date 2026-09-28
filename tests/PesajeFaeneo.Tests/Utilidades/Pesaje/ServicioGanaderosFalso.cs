using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Catalogos.Busqueda;
using PesajeFaeneo.Application.Catalogos.Dtos;

namespace PesajeFaeneo.Tests.Utilidades.Pesaje;

public class ServicioGanaderosFalso : IServicioGanaderos
{
    public List<GanaderoListado> Ganaderos { get; } = new();
    public List<ElementoBusqueda> ElementosBusqueda { get; } = new();
    public Func<CrearGanadero, Task<int>>? AlCrearRapidoAsync { get; set; }
    public int IdCreadoRapido { get; set; } = 1;

    public Task<IReadOnlyList<GanaderoListado>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<GanaderoListado>>(Ganaderos);

    public Task<GanaderoListado> ObtenerAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Ganaderos.Single(g => g.Id == id));

    public Task<IReadOnlyList<ElementoBusqueda>> ListarParaBusquedaAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ElementoBusqueda>>(ElementosBusqueda.ToList());

    public Task<int> CrearAsync(CrearGanadero dto, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public async Task<int> CrearRapidoAsync(CrearGanadero dto, CancellationToken cancellationToken = default)
    {
        if (AlCrearRapidoAsync != null)
        {
            return await AlCrearRapidoAsync(dto);
        }
        return IdCreadoRapido;
    }

    public Task EditarAsync(EditarGanadero dto, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task CambiarEstadoAsync(int id, bool activo, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
