using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Catalogos.Dtos;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms.Formularios.Catalogos;
using PesajeFaeneo.WinForms.Infraestructura;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmFleterosTests
{
    private class FalsoServicioFleteros : IServicioFleteros
    {
        public List<FleteroListado> Fleteros { get; } = new();

        public Task<IReadOnlyList<FleteroListado>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default)
        {
            var resultado = incluirInactivos ? Fleteros : Fleteros.Where(f => f.Activo).ToList();
            return Task.FromResult<IReadOnlyList<FleteroListado>>(resultado);
        }

        public Task<FleteroListado> ObtenerAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Fleteros.Single(f => f.Id == id));

        public Task<IReadOnlyList<PesajeFaeneo.Application.Catalogos.Busqueda.ElementoBusqueda>> ListarParaBusquedaAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PesajeFaeneo.Application.Catalogos.Busqueda.ElementoBusqueda>>(Fleteros.Where(f => f.Activo)
                .Select(f => new PesajeFaeneo.Application.Catalogos.Busqueda.ElementoBusqueda(f.Id, f.Codigo.ToString(), f.Nombre, f.Celular, f.Activo)).ToList());

        public Task<IReadOnlyList<FleteroListado>> ListarActivosAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FleteroListado>>(Fleteros.Where(f => f.Activo).ToList());

        public Task<int> CrearAsync(CrearFletero dto, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task EditarAsync(EditarFletero dto, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task CambiarEstadoAsync(int id, bool activo, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class FalsaFormFactory : IFormFactory
    {
        public T Crear<T>() where T : Form => throw new NotImplementedException();
    }

    [Fact]
    public void FrmFleteros_ColumnaCodigoMuestraElNumeroDeActivosEInactivos_SinLimiteDeFleteros()
    {
        HiloSta.Ejecutar(() =>
        {
            var servicio = new FalsoServicioFleteros();
            servicio.Fleteros.Add(new FleteroListado(1, 2, "CARLOS PEREZ", "04141234567", Activo: true));
            servicio.Fleteros.Add(new FleteroListado(2, 3, "LUIS RAMIREZ", "04120000000", Activo: false));
            // F10 (O5): ya no hay tope de 4 fleteros; la lista muestra todos.
            for (var i = 4; i <= 12; i++)
            {
                servicio.Fleteros.Add(new FleteroListado(i, i, $"FLETERO {i}", "70000000", Activo: true));
            }

            using var form = new FrmFleteros(servicio, new FalsaFormFactory());
            form.Show();

            form.MostrarInactivosParaTest = true;
            form.CargarParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal(11, form.GrillaParaTest.Rows.Count);
            Assert.Equal("Código", form.GrillaParaTest.Columns["ColCodigo"]!.HeaderText);

            var filaActivo = form.GrillaParaTest.Rows[0].DataBoundItem;
            Assert.NotNull(filaActivo);
            Assert.Equal("2", (string)filaActivo!.GetType().GetProperty("CodigoTexto")!.GetValue(filaActivo)!);

            // El inactivo conserva su código (ya no se muestra un guion).
            var filaInactivo = form.GrillaParaTest.Rows[1].DataBoundItem;
            Assert.NotNull(filaInactivo);
            Assert.Equal("3", (string)filaInactivo!.GetType().GetProperty("CodigoTexto")!.GetValue(filaInactivo)!);
        });
    }
}
