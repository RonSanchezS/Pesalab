using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Application.Seguridad.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms.Formularios.Seguridad;
using PesajeFaeneo.WinForms.Infraestructura;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmUsuariosTests
{
    private class FalsoServicioUsuarios : IServicioUsuarios
    {
        public List<UsuarioListado> Usuarios { get; } = new();

        public Task<IReadOnlyList<UsuarioListado>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default)
        {
            var resultado = incluirInactivos ? Usuarios : Usuarios.Where(u => u.Activo).ToList();
            return Task.FromResult<IReadOnlyList<UsuarioListado>>(resultado);
        }

        public Task<UsuarioListado> ObtenerAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Usuarios.Single(u => u.Id == id));

        public Task<int> CrearAsync(CrearUsuario dto, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task EditarAsync(EditarUsuario dto, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task CambiarEstadoAsync(int id, bool activo, CancellationToken cancellationToken = default)
        {
            var indice = Usuarios.FindIndex(u => u.Id == id);
            Usuarios[indice] = Usuarios[indice] with { Activo = activo };
            return Task.CompletedTask;
        }

        public Task RestablecerPasswordAsync(int id, string nueva, string confirmacion, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
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
            var servicio = new FalsoServicioUsuarios();
            servicio.Usuarios.Add(new UsuarioListado(1, "operador1", "Operador Uno", Rol.Operador, Activo: true, Bloqueado: false, UltimoAcceso: null));

            using var form = new FrmUsuarios(servicio, new FalsaFormFactory(), TimeProvider.System);
            form.Show();

            // Con inactivos visibles, el usuario debe seguir en la grilla tras desactivarlo
            // (solo cambia su columna "Activo"), lo que aísla esta prueba del filtrado propio
            // de "Mostrar usuarios inactivos" y prueba únicamente el refresco tras la operación.
            form.MostrarInactivosParaTest = true;

            // Simula la carga inicial que dispara FrmUsuarios en su evento Shown.
            form.CargarUsuariosParaTestAsync().GetAwaiter().GetResult();

            form.SeleccionarFilaParaTest(1);

            // Antes de la corrección, CargarUsuariosAsync se invocaba anidada dentro de otro
            // EjecutarAsync y el guard de reentrada de FormBase la descartaba en silencio: la
            // grilla se quedaba mostrando "Activo = Sí" aunque el servicio ya hubiera desactivado
            // al usuario.
            form.CambiarEstadoSeleccionadoParaTestAsync().GetAwaiter().GetResult();

            Assert.Single(servicio.Usuarios);
            Assert.False(servicio.Usuarios[0].Activo);

            var filaGrilla = form.GrillaParaTest.Rows[0].DataBoundItem;
            Assert.NotNull(filaGrilla);
            var activoTexto = (string)filaGrilla!.GetType().GetProperty("ActivoTexto")!.GetValue(filaGrilla)!;
            Assert.Equal("No", activoTexto);
        });
    }
}
