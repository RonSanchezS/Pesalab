using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Catalogos.Dtos;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms.Formularios.Catalogos;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmClienteEdicionTests
{
    private class FalsoServicioClientes : IServicioClientes
    {
        public List<CrearCliente> Creados { get; } = new();
        public List<CrearCliente> CreadosRapidos { get; } = new();
        public List<EditarCliente> Editados { get; } = new();

        public Task<IReadOnlyList<ClienteListado>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ClienteListado> ObtenerAsync(int id, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<PesajeFaeneo.Application.Catalogos.Busqueda.ElementoBusqueda>> ListarParaBusquedaAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PesajeFaeneo.Application.Catalogos.Busqueda.ElementoBusqueda>>([]);

        public Task<int> CrearAsync(CrearCliente dto, CancellationToken cancellationToken = default)
        {
            Creados.Add(dto);
            return Task.FromResult(9);
        }

        public Task<int> CrearRapidoAsync(CrearCliente dto, CancellationToken cancellationToken = default)
        {
            CreadosRapidos.Add(dto);
            return Task.FromResult(10);
        }

        public Task EditarAsync(EditarCliente dto, CancellationToken cancellationToken = default)
        {
            Editados.Add(dto);
            return Task.CompletedTask;
        }

        public Task CambiarEstadoAsync(int id, bool activo, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    [Fact]
    public void FrmClienteEdicion_ConfigurarAlta_CodigoSoloLecturaYSeAsignaAlGuardar()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeServicio = new FalsoServicioClientes();
            using var form = new FrmClienteEdicion(fakeServicio);
            form.Show();

            form.ConfigurarAlta();

            Assert.True(form.TxtCodigo.ReadOnly);
            Assert.False(form.TxtCodigo.TabStop);
            Assert.Equal(FrmClienteEdicion.TextoCodigoAlta, form.TxtCodigo.Text);
            Assert.Same(form.TxtNombre, form.ActiveControl);

            form.TxtNombre.Text = "CLIENTE NUEVO";
            form.TxtDireccion.Text = "CALLE 1";
            form.GuardarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(new CrearCliente("CLIENTE NUEVO", "CALLE 1"), Assert.Single(fakeServicio.Creados));
            Assert.Equal(9, form.IdGuardado);
        });
    }

    [Fact]
    public void FrmClienteEdicion_ConfigurarEdicion_MuestraElCodigoRealYNoLoEnvia()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeServicio = new FalsoServicioClientes();
            using var form = new FrmClienteEdicion(fakeServicio);
            form.Show();

            form.ConfigurarEdicion(new ClienteListado(4, 215, "CLIENTE UNO", "DIR UNO", true));

            Assert.Equal("215", form.TxtCodigo.Text);
            Assert.True(form.TxtCodigo.ReadOnly);

            form.TxtNombre.Text = "CLIENTE UNO EDITADO";
            form.GuardarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(new EditarCliente(4, "CLIENTE UNO EDITADO", "DIR UNO"), Assert.Single(fakeServicio.Editados));
        });
    }

    [Fact]
    public void FrmClienteEdicion_ConfigurarAltaRapida_PrellenaElNombreConElTextoBuscado_YNoPideCodigo()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeServicio = new FalsoServicioClientes();
            using var form = new FrmClienteEdicion(fakeServicio);
            form.Show();

            form.ConfigurarAltaRapida("Juan Perez");

            Assert.Equal("JUAN PEREZ", form.TxtNombre.Text);
            Assert.Equal(FrmClienteEdicion.TextoCodigoAlta, form.TxtCodigo.Text);
            Assert.Equal("Alta rápida de cliente", form.Text);
            Assert.Same(form.TxtNombre, form.ActiveControl);

            form.GuardarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(new CrearCliente("JUAN PEREZ", ""), Assert.Single(fakeServicio.CreadosRapidos));
            Assert.Equal(10, form.IdGuardado);
        });
    }
}
