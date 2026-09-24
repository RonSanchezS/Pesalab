using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Application.Seguridad.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms.Formularios.Seguridad;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmUsuarioEdicionTests
{
    private class FalsoServicioUsuarios : IServicioUsuarios
    {
        public Task<IReadOnlyList<UsuarioListado>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UsuarioListado>>([]);

        public Task<UsuarioListado> ObtenerAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(new UsuarioListado(id, "test", "Test User", Rol.Operador, true, false, null));

        public Task<int> CrearAsync(CrearUsuario dto, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task EditarAsync(EditarUsuario dto, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CambiarEstadoAsync(int id, bool activo, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RestablecerPasswordAsync(int id, string nueva, string confirmacion, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public void FrmUsuarioEdicion_ModoAlta_CamposPasswordVisibles_YNombreUsuarioEditable()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeServicio = new FalsoServicioUsuarios();
            var sesion = new SesionActual();
            using var form = new FrmUsuarioEdicion(fakeServicio, sesion);
            form.Show();

            form.ConfigurarAlta();

            Assert.False(form.txtNombreUsuario.ReadOnly);
            Assert.True(form.lblPassword.Visible);
            Assert.True(form.txtPassword.Visible);
            Assert.True(form.lblConfirmacion.Visible);
            Assert.True(form.txtConfirmacion.Visible);
            Assert.True(form.cmbRol.Enabled);
        });
    }

    [Fact]
    public void FrmUsuarioEdicion_ModoEdicion_CamposPasswordOcultos_YNombreUsuarioSoloLectura()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeServicio = new FalsoServicioUsuarios();
            var sesion = new SesionActual();
            sesion.Iniciar(new UsuarioSesion(1, "admin", "Admin", Rol.Administrador, false));

            using var form = new FrmUsuarioEdicion(fakeServicio, sesion);
            form.Show();

            var usuarioAEditar = new UsuarioListado(2, "operador", "Operador Uno", Rol.Operador, true, false, null);
            form.ConfigurarEdicion(usuarioAEditar);

            Assert.True(form.txtNombreUsuario.ReadOnly);
            Assert.Equal("operador", form.txtNombreUsuario.Text);
            Assert.Equal("Operador Uno", form.txtNombreCompleto.Text);
            Assert.False(form.lblPassword.Visible);
            Assert.False(form.txtPassword.Visible);
            Assert.False(form.lblConfirmacion.Visible);
            Assert.False(form.txtConfirmacion.Visible);
            Assert.True(form.cmbRol.Enabled);
        });
    }

    [Fact]
    public void FrmUsuarioEdicion_ModoEdicion_PropioUsuario_RolDeshabilitado()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeServicio = new FalsoServicioUsuarios();
            var sesion = new SesionActual();
            sesion.Iniciar(new UsuarioSesion(1, "admin", "Admin", Rol.Administrador, false));

            using var form = new FrmUsuarioEdicion(fakeServicio, sesion);
            form.Show();

            var usuarioPropio = new UsuarioListado(1, "admin", "Admin Modificado", Rol.Administrador, true, false, null);
            form.ConfigurarEdicion(usuarioPropio);

            // Al editarse a sí mismo, el combo de rol debe quedar deshabilitado
            Assert.False(form.cmbRol.Enabled);
        });
    }
}
