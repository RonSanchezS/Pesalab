using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Catalogos.Dtos;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms.Formularios.Catalogos;
using PesajeFaeneo.WinForms.Infraestructura;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmGanaderosTests
{
    private class FalsoServicioGanaderos : IServicioGanaderos
    {
        public List<GanaderoListado> Ganaderos { get; } = new();

        public Task<IReadOnlyList<GanaderoListado>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default)
        {
            var resultado = incluirInactivos ? Ganaderos : Ganaderos.Where(g => g.Activo).ToList();
            return Task.FromResult<IReadOnlyList<GanaderoListado>>(resultado);
        }

        public Task<GanaderoListado> ObtenerAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Ganaderos.Single(g => g.Id == id));

        public Task<IReadOnlyList<PesajeFaeneo.Application.Catalogos.Busqueda.ElementoBusqueda>> ListarParaBusquedaAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PesajeFaeneo.Application.Catalogos.Busqueda.ElementoBusqueda>>([]);

        public Task<int> CrearAsync(CrearGanadero dto, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<int> CrearRapidoAsync(CrearGanadero dto, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task EditarAsync(EditarGanadero dto, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task CambiarEstadoAsync(int id, bool activo, CancellationToken cancellationToken = default)
        {
            var indice = Ganaderos.FindIndex(g => g.Id == id);
            Ganaderos[indice] = Ganaderos[indice] with { Activo = activo };
            return Task.CompletedTask;
        }
    }

    private class FalsaFormFactory : IFormFactory
    {
        public T Crear<T>() where T : Form => throw new NotImplementedException();
    }

    [Fact]
    public void CambiarEstadoSeleccionado_RecargaLaGrillaConElEstadoActualizado()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new FalsoServicioGanaderos();
            servicio.Ganaderos.Add(new GanaderoListado(1, "AH", "JUAN PEREZ", Activo: true));

            using var form = new FrmGanaderos(servicio, new FalsaFormFactory());
            form.Show();

            form.MostrarInactivosParaTest = true;
            form.CargarParaTestAsync().GetAwaiter().GetResult();
            form.SeleccionarFilaParaTest(1);

            form.CambiarEstadoSeleccionadoParaTestAsync().GetAwaiter().GetResult();

            Assert.Single(servicio.Ganaderos);
            Assert.False(servicio.Ganaderos[0].Activo);

            var filaGrilla = form.GrillaParaTest.Rows[0].DataBoundItem;
            Assert.NotNull(filaGrilla);
            var activoTexto = (string)filaGrilla!.GetType().GetProperty("ActivoTexto")!.GetValue(filaGrilla)!;
            Assert.Equal("No", activoTexto);
        });
    }
}
