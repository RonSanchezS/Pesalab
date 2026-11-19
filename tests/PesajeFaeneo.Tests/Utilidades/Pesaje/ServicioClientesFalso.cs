using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Catalogos.Busqueda;
using PesajeFaeneo.Application.Catalogos.Dtos;

namespace PesajeFaeneo.Tests.Utilidades.Pesaje;

public class ServicioClientesFalso : IServicioClientes
{
    public List<ClienteListado> Clientes { get; } = new();
    public List<ElementoBusqueda> ElementosBusqueda { get; } = new();
    public Func<CrearCliente, Task<int>>? AlCrearRapidoAsync { get; set; }
    public int IdCreadoRapido { get; set; } = 1;

    public Task<IReadOnlyList<ClienteListado>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ClienteListado>>(Clientes);

    public Task<ClienteListado> ObtenerAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Clientes.Single(c => c.Id == id));

    public Task<IReadOnlyList<ElementoBusqueda>> ListarParaBusquedaAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ElementoBusqueda>>(ElementosBusqueda.ToList());

    public Task<int> CrearAsync(CrearCliente dto, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public async Task<int> CrearRapidoAsync(CrearCliente dto, CancellationToken cancellationToken = default)
    {
        if (AlCrearRapidoAsync != null)
        {
            return await AlCrearRapidoAsync(dto);
        }
        return IdCreadoRapido;
    }

    public Task EditarAsync(EditarCliente dto, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task CambiarEstadoAsync(int id, bool activo, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
