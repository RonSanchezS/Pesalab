using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Catalogos.Dtos;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms.Formularios.Catalogos;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmGanaderoEdicionTests
{
    private class FalsoServicioGanaderos : IServicioGanaderos
    {
        public bool LanzarErrorNombre { get; set; }
        public List<CrearGanadero> Creados { get; } = new();
        public List<CrearGanadero> CreadosRapidos { get; } = new();
        public List<EditarGanadero> Editados { get; } = new();

        public Task<IReadOnlyList<GanaderoListado>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<GanaderoListado> ObtenerAsync(int id, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<PesajeFaeneo.Application.Catalogos.Busqueda.ElementoBusqueda>> ListarParaBusquedaAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PesajeFaeneo.Application.Catalogos.Busqueda.ElementoBusqueda>>([]);

        public Task<int> CrearAsync(CrearGanadero dto, CancellationToken cancellationToken = default)
        {
            if (LanzarErrorNombre)
            {
                throw new ValidacionException(CamposCatalogo.Nombre, "El nombre no puede estar vacío.");
            }

            Creados.Add(dto);
            return Task.FromResult(5);
        }

        public Task<int> CrearRapidoAsync(CrearGanadero dto, CancellationToken cancellationToken = default)
        {
            CreadosRapidos.Add(dto);
            return Task.FromResult(6);
        }

        public Task EditarAsync(EditarGanadero dto, CancellationToken cancellationToken = default)
        {
            Editados.Add(dto);
            return Task.CompletedTask;
        }

        public Task CambiarEstadoAsync(int id, bool activo, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    [Fact]
    public void FrmGanaderoEdicion_ErrorValidacion_MuestraErrorEnControlYNoCierraFormulario()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeServicio = new FalsoServicioGanaderos { LanzarErrorNombre = true };
            using var form = new FrmGanaderoEdicion(fakeServicio);
            form.Show();

            form.ConfigurarAlta();
            form.TxtNombre.Text = string.Empty;

            form.GuardarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(DialogResult.None, form.DialogResult);
            var error = form._errorProvider.GetError(form.TxtNombre);
            Assert.Equal("El nombre no puede estar vacío.", error);
        });
    }

    [Fact]
    public void FrmGanaderoEdicion_ConfigurarAlta_CodigoSoloLecturaYSeAsignaAlGuardar()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeServicio = new FalsoServicioGanaderos();
            using var form = new FrmGanaderoEdicion(fakeServicio);
            form.Show();

            form.ConfigurarAlta();

            Assert.True(form.TxtCodigo.ReadOnly);
            Assert.False(form.TxtCodigo.TabStop);
            Assert.Equal(FrmGanaderoEdicion.TextoCodigoAlta, form.TxtCodigo.Text);
            Assert.Same(form.TxtNombre, form.ActiveControl);

            form.TxtNombre.Text = "JUAN PEREZ";
            form.GuardarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(new CrearGanadero("JUAN PEREZ"), Assert.Single(fakeServicio.Creados));
            Assert.Equal(5, form.IdGuardado);
        });
    }

    [Fact]
    public void FrmGanaderoEdicion_ConfigurarEdicion_MuestraElCodigoRealYNoLoEnvia()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeServicio = new FalsoServicioGanaderos();
            using var form = new FrmGanaderoEdicion(fakeServicio);
            form.Show();

            form.ConfigurarEdicion(new GanaderoListado(3, "AH", "JUAN PEREZ", true)); // código legado alfanumérico

            Assert.Equal("AH", form.TxtCodigo.Text);
            Assert.True(form.TxtCodigo.ReadOnly);

            form.TxtNombre.Text = "JUAN PEREZ LOPEZ";
            form.GuardarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(new EditarGanadero(3, "JUAN PEREZ LOPEZ"), Assert.Single(fakeServicio.Editados));
        });
    }

    [Fact]
    public void FrmGanaderoEdicion_ConfigurarAltaRapida_PrellenaElNombreConElTextoBuscado_YNoPideCodigo()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeServicio = new FalsoServicioGanaderos();
            using var form = new FrmGanaderoEdicion(fakeServicio);
            form.Show();

            // Antes un texto corto se interpretaba como marca; ahora siempre es el nombre.
            form.ConfigurarAltaRapida("ah");

            Assert.Equal("AH", form.TxtNombre.Text);
            Assert.Equal(FrmGanaderoEdicion.TextoCodigoAlta, form.TxtCodigo.Text);
            Assert.Equal("Alta rápida de ganadero", form.Text);
            Assert.Same(form.TxtNombre, form.ActiveControl);

            form.GuardarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(new CrearGanadero("AH"), Assert.Single(fakeServicio.CreadosRapidos));
            Assert.Equal(6, form.IdGuardado);
        });
    }
}
