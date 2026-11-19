using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Catalogos.Busqueda;
using PesajeFaeneo.Application.Catalogos.Dtos;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms.Formularios.Catalogos;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmFleteroEdicionTests
{
    private class FalsoServicioFleteros : IServicioFleteros
    {
        public List<CrearFletero> Creados { get; } = new();
        public List<EditarFletero> Editados { get; } = new();

        public Task<IReadOnlyList<FleteroListado>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<FleteroListado> ObtenerAsync(int id, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<FleteroListado>> ListarActivosAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<ElementoBusqueda>> ListarParaBusquedaAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<int> CrearAsync(CrearFletero dto, CancellationToken cancellationToken = default)
        {
            Creados.Add(dto);
            return Task.FromResult(7);
        }

        public Task EditarAsync(EditarFletero dto, CancellationToken cancellationToken = default)
        {
            Editados.Add(dto);
            return Task.CompletedTask;
        }

        public Task CambiarEstadoAsync(int id, bool activo, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    [Fact]
    public void FrmFleteroEdicion_ConfigurarAlta_CodigoSoloLecturaYSeAsignaAlGuardar()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeServicio = new FalsoServicioFleteros();
            using var form = new FrmFleteroEdicion(fakeServicio);
            form.Show();

            form.ConfigurarAlta();

            Assert.True(form.TxtCodigo.ReadOnly);
            Assert.False(form.TxtCodigo.TabStop);
            Assert.Equal(FrmFleteroEdicion.TextoCodigoAlta, form.TxtCodigo.Text);

            form.TxtNombre.Text = "MIGUEL TORRES";
            form.TxtCelular.Text = "70012345";
            form.GuardarParaTestAsync().GetAwaiter().GetResult();

            // El alta ya no envía ningún código: lo asigna el servicio.
            var dto = Assert.Single(fakeServicio.Creados);
            Assert.Equal(new CrearFletero("MIGUEL TORRES", "70012345"), dto);
            Assert.Equal(7, form.IdGuardado);
        });
    }

    [Fact]
    public void FrmFleteroEdicion_ConfigurarEdicion_MuestraElCodigoRealYNoLoEnvia()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeServicio = new FalsoServicioFleteros();
            using var form = new FrmFleteroEdicion(fakeServicio);
            form.Show();

            var dto = new FleteroListado(5, 14, "MIGUEL TORRES", "04129876543", true);
            form.ConfigurarEdicion(dto);

            Assert.Equal("14", form.TxtCodigo.Text);
            Assert.True(form.TxtCodigo.ReadOnly);
            Assert.Equal("MIGUEL TORRES", form.TxtNombre.Text);
            Assert.Equal("04129876543", form.TxtCelular.Text);

            form.TxtNombre.Text = "MIGUEL TORRES DIAZ";
            form.GuardarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(new EditarFletero(5, "MIGUEL TORRES DIAZ", "04129876543"), Assert.Single(fakeServicio.Editados));
        });
    }
}
