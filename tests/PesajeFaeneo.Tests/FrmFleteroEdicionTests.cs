using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Catalogos.Dtos;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms.Formularios.Catalogos;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmFleteroEdicionTests
{
    private class FalsoServicioFleteros : IServicioFleteros
    {
        public Task<IReadOnlyList<FleteroListado>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<FleteroListado> ObtenerAsync(int id, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<FleteroListado>> ListarActivosAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<int> CrearAsync(CrearFletero dto, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task EditarAsync(EditarFletero dto, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task CambiarEstadoAsync(int id, bool activo, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    [Fact]
    public void FrmFleteroEdicion_ConfigurarAlta_AsignaOrdenSugerido()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeServicio = new FalsoServicioFleteros();
            using var form = new FrmFleteroEdicion(fakeServicio);
            form.Show();

            form.ConfigurarAlta(3);

            Assert.Equal(3, (int)form.CmbOrden.SelectedValue!);
        });
    }

    [Fact]
    public void FrmFleteroEdicion_ConfigurarEdicion_CargaDatosDto()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeServicio = new FalsoServicioFleteros();
            using var form = new FrmFleteroEdicion(fakeServicio);
            form.Show();

            var dto = new FleteroListado(5, 4, "MIGUEL TORRES", "04129876543", true);
            form.ConfigurarEdicion(dto);

            Assert.Equal(4, (int)form.CmbOrden.SelectedValue!);
            Assert.Equal("MIGUEL TORRES", form.TxtNombre.Text);
            Assert.Equal("04129876543", form.TxtCelular.Text);
        });
    }
}
