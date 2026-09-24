using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms.Formularios.Seguridad;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FrmLoginTests
{
    private class FalsoServicioAutenticacion : IServicioAutenticacion
    {
        public ResultadoLogin ResultadoARetornar { get; set; } =
            new(ResultadoLoginTipo.CredencialesInvalidas, "Usuario o contraseña incorrectos.");

        public Task<ResultadoLogin> IniciarSesionAsync(string? nombreUsuario, string? password, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultadoARetornar);
        }

        public Task CerrarSesionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CambiarPasswordPropiaAsync(string actual, string nueva, string confirmacion, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public void FrmLogin_CredencialesInvalidas_MuestraMensaje_LimpiaPassword_YNoCierraFormulario()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeAuth = new FalsoServicioAutenticacion();
            using var form = new FrmLogin(fakeAuth);
            form.Show();

            form.txtUsuario.Text = "usuario_erroneo";
            form.txtPassword.Text = "password_erroneo";

            form.IngresarAsync().GetAwaiter().GetResult();

            Assert.Equal("Usuario o contraseña incorrectos.", form.lblMensaje.Text);
            Assert.Equal(string.Empty, form.txtPassword.Text);
            Assert.Equal(DialogResult.None, form.DialogResult);
            Assert.False(form.IsDisposed);
        });
    }

    [Fact]
    public void FrmLogin_LoginExitoso_EstableceDialogResultOkYCierra()
    {
        HiloSta.Ejecutar(() =>
        {
            var fakeAuth = new FalsoServicioAutenticacion
            {
                ResultadoARetornar = new ResultadoLogin(ResultadoLoginTipo.Exitoso, "OK", DebeCambiarPassword: true)
            };
            using var form = new FrmLogin(fakeAuth);
            form.Show();

            form.txtUsuario.Text = "admin";
            form.txtPassword.Text = "admin123";

            form.IngresarAsync().GetAwaiter().GetResult();

            Assert.Equal(DialogResult.OK, form.DialogResult);
            Assert.True(form.DebeCambiarPassword);
        });
    }
}
