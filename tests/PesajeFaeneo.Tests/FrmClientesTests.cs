using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Catalogos.Dtos;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms.Formularios.Catalogos;
using PesajeFaeneo.WinForms.Infraestructura;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmClientesTests
{
    private class FalsoServicioClientes : IServicioClientes
    {
        public List<ClienteListado> Clientes { get; } = new();

        public Task<IReadOnlyList<ClienteListado>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default)
        {
            var resultado = incluirInactivos ? Clientes : Clientes.Where(c => c.Activo).ToList();
            return Task.FromResult<IReadOnlyList<ClienteListado>>(resultado);
        }

        public Task<ClienteListado> ObtenerAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Clientes.Single(c => c.Id == id));

        public Task<IReadOnlyList<PesajeFaeneo.Application.Catalogos.Busqueda.ElementoBusqueda>> ListarParaBusquedaAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PesajeFaeneo.Application.Catalogos.Busqueda.ElementoBusqueda>>([]);

        public Task<int> CrearAsync(CrearCliente dto, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<int> CrearRapidoAsync(CrearCliente dto, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task EditarAsync(EditarCliente dto, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task CambiarEstadoAsync(int id, bool activo, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<int> SugerirSiguienteCodigoAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(1);
    }

    private class FalsaFormFactory : IFormFactory
    {
        public T Crear<T>() where T : Form => throw new NotImplementedException();
    }

    [Fact]
    public void FrmClientes_GrillaMuestraDatosYBusquedaSeleccionaFila()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new FalsoServicioClientes();
            servicio.Clientes.Add(new ClienteListado(1, 101, "CARNICERIA EL SOL", "AV PRINCIPAL 123", Activo: true));
            servicio.Clientes.Add(new ClienteListado(2, 102, "FRIGORIFICO CENTRAL", "CALLE 45 678", Activo: true));

            using var form = new FrmClientes(servicio, new FalsaFormFactory());
            form.Show();

            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(2, form.GrillaParaTest.Rows.Count);

            var fila1 = form.GrillaParaTest.Rows[0].DataBoundItem;
            Assert.NotNull(fila1);
            Assert.Equal(101, fila1!.GetType().GetProperty("Codigo")!.GetValue(fila1));
            Assert.Equal("CARNICERIA EL SOL", fila1.GetType().GetProperty("Nombre")!.GetValue(fila1));
            Assert.Equal("AV PRINCIPAL 123", fila1.GetType().GetProperty("Direccion")!.GetValue(fila1));

            // Seleccionar cliente 2 mediante el control de búsqueda
            form.BusquedaParaTest.Seleccionar(2);

            Assert.NotNull(form.GrillaParaTest.CurrentRow);
            var filaSeleccionada = form.GrillaParaTest.CurrentRow!.DataBoundItem;
            Assert.Equal(2, filaSeleccionada!.GetType().GetProperty("Id")!.GetValue(filaSeleccionada));
        });
    }
}
