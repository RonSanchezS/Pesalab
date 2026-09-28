using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Catalogos.Dtos;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms.Formularios.Catalogos;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmGanaderoEdicionTests
{
    private class FalsoServicioGanaderosConError : IServicioGanaderos
    {
        public Task<IReadOnlyList<GanaderoListado>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<GanaderoListado> ObtenerAsync(int id, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<PesajeFaeneo.Application.Catalogos.Busqueda.ElementoBusqueda>> ListarParaBusquedaAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PesajeFaeneo.Application.Catalogos.Busqueda.ElementoBusqueda>>([]);

        public Task<int> CrearAsync(CrearGanadero dto, CancellationToken cancellationToken = default) =>
            throw new ValidacionException(CamposCatalogo.Marca, "La marca no puede estar vacía.");

        public Task<int> CrearRapidoAsync(CrearGanadero dto, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task EditarAsync(EditarGanadero dto, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task CambiarEstadoAsync(int id, bool activo, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    [Fact]
    public void FrmGanaderoEdicion_ErrorValidacion_MuestraErrorEnControlYNoCierraFormulario()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeServicio = new FalsoServicioGanaderosConError();
            using var form = new FrmGanaderoEdicion(fakeServicio);
            form.Show();

            form.ConfigurarAlta();
            form.TxtMarca.Text = string.Empty;
            form.TxtNombre.Text = "NOMBRE VALIDO";

            form.GuardarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(DialogResult.None, form.DialogResult);
            var error = form._errorProvider.GetError(form.TxtMarca);
            Assert.Equal("La marca no puede estar vacía.", error);
        });
    }

    [Fact]
    public void FrmGanaderoEdicion_ConfigurarAltaRapida_ConMarcaValida_AsignaMarcaYFocoEnNombre()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeServicio = new FalsoServicioGanaderosConError();
            using var form = new FrmGanaderoEdicion(fakeServicio);
            form.Show();

            form.ConfigurarAltaRapida("ah");

            Assert.Equal("AH", form.TxtMarca.Text);
            Assert.Equal(string.Empty, form.TxtNombre.Text);
            Assert.Equal("Alta rápida de ganadero", form.Text);
            Assert.Same(form.TxtNombre, form.ActiveControl);
        });
    }

    [Fact]
    public void FrmGanaderoEdicion_ConfigurarAltaRapida_ConTextoNoMarca_AsignaNombreYFocoEnMarca()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeServicio = new FalsoServicioGanaderosConError();
            using var form = new FrmGanaderoEdicion(fakeServicio);
            form.Show();

            form.ConfigurarAltaRapida("Juan Perez");

            Assert.Equal(string.Empty, form.TxtMarca.Text);
            Assert.Equal("JUAN PEREZ", form.TxtNombre.Text);
            Assert.Equal("Alta rápida de ganadero", form.Text);
            Assert.Same(form.TxtMarca, form.ActiveControl);
        });
    }
}
