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
        public Task<IReadOnlyList<ClienteListado>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ClienteListado> ObtenerAsync(int id, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<PesajeFaeneo.Application.Catalogos.Busqueda.ElementoBusqueda>> ListarParaBusquedaAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PesajeFaeneo.Application.Catalogos.Busqueda.ElementoBusqueda>>([]);

        public Task<int> CrearAsync(CrearCliente dto, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task EditarAsync(EditarCliente dto, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task CambiarEstadoAsync(int id, bool activo, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<int> SugerirSiguienteCodigoAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(1);
    }

    [Fact]
    public void FrmClienteEdicion_ConfigurarAlta_AsignaCodigoSugerido()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeServicio = new FalsoServicioClientes();
            using var form = new FrmClienteEdicion(fakeServicio);
            form.Show();

            form.ConfigurarAlta(215);

            Assert.Equal(215, form.NumCodigo.Value);
        });
    }
}
