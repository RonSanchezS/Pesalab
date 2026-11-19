using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Catalogos.Busqueda;
using PesajeFaeneo.Application.Catalogos.Dtos;

namespace PesajeFaeneo.Tests.Utilidades.Pesaje;

public class ServicioFleterosFalso : IServicioFleteros
{
    public List<FleteroListado> Fleteros { get; } = new();
    public List<ElementoBusqueda> ElementosBusqueda { get; } = new();
    public int VecesListarParaBusqueda { get; private set; }

    public Task<IReadOnlyList<FleteroListado>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FleteroListado>>(Fleteros);

    public Task<FleteroListado> ObtenerAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Fleteros.Single(f => f.Id == id));

    public Task<IReadOnlyList<FleteroListado>> ListarActivosAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FleteroListado>>(Fleteros.Where(f => f.Activo).ToList());

    public Task<IReadOnlyList<ElementoBusqueda>> ListarParaBusquedaAsync(CancellationToken cancellationToken = default)
    {
        VecesListarParaBusqueda++;
        return Task.FromResult<IReadOnlyList<ElementoBusqueda>>(ElementosBusqueda.ToList());
    }

    public Task<int> CrearAsync(CrearFletero dto, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task EditarAsync(EditarFletero dto, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task CambiarEstadoAsync(int id, bool activo, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
