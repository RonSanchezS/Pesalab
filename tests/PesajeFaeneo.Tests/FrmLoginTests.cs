using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Pesaje;
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
            using var form = new FrmLogin(fakeAuth, new ServicioParametrosFalso());
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
            using var form = new FrmLogin(fakeAuth, new ServicioParametrosFalso());
            form.Show();

            form.txtUsuario.Text = "admin";
            form.txtPassword.Text = "admin123";

            form.IngresarAsync().GetAwaiter().GetResult();

            Assert.Equal(DialogResult.OK, form.DialogResult);
            Assert.True(form.DebeCambiarPassword);
        });
    }

    private static string CrearLogoTemporal()
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"logo-login-{Guid.NewGuid():N}.png");
        using var bmp = new Bitmap(40, 20);
        bmp.Save(ruta, System.Drawing.Imaging.ImageFormat.Png);
        return ruta;
    }

    [Fact]
    public void FrmLogin_Encabezado_ConLogoYNombre_MuestraAmbosYNoBloqueaElArchivo()
    {
        HiloSta.Ejecutar(() =>
        {
            var ruta = CrearLogoTemporal();
            try
            {
                var parametros = new ServicioParametrosFalso();
                parametros.Parametros = parametros.Parametros with { NombreEmpresa = "FRIGORIFICO CENTRAL", RutaLogoCompleta = ruta };
                using var form = new FrmLogin(new FalsoServicioAutenticacion(), parametros);

                // Antes de cargar: título genérico y sin logo.
                Assert.Equal("Capataz", form.lblEmpresa.Text);
                Assert.Null(form.picLogo.Image);

                form.CargarEncabezadoParaTestAsync().GetAwaiter().GetResult();

                Assert.Equal("FRIGORIFICO CENTRAL", form.lblEmpresa.Text);
                Assert.NotNull(form.picLogo.Image);

                File.Delete(ruta);
                Assert.False(File.Exists(ruta)); // el login no deja el logo bloqueado
            }
            finally
            {
                if (File.Exists(ruta))
                {
                    File.Delete(ruta);
                }
            }
        });
    }

    [Fact]
    public void FrmLogin_Encabezado_SinLogo_MuestraSoloElNombre()
    {
        HiloSta.Ejecutar(() =>
        {
            var parametros = new ServicioParametrosFalso();
            parametros.Parametros = parametros.Parametros with { NombreEmpresa = "MATADERO SUR", RutaLogoCompleta = null };
            using var form = new FrmLogin(new FalsoServicioAutenticacion(), parametros);

            form.CargarEncabezadoParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal("MATADERO SUR", form.lblEmpresa.Text);
            Assert.Null(form.picLogo.Image);
            Assert.False(form.picLogo.Visible);
        });
    }

    [Fact]
    public void FrmLogin_Encabezado_LogoDanado_SeIgnoraYMuestraElNombre()
    {
        HiloSta.Ejecutar(() =>
        {
            var ruta = Path.Combine(Path.GetTempPath(), $"logo-danado-{Guid.NewGuid():N}.png");
            File.WriteAllBytes(ruta, new byte[] { 1, 2, 3, 4, 5 });
            try
            {
                var parametros = new ServicioParametrosFalso();
                parametros.Parametros = parametros.Parametros with { NombreEmpresa = "MATADERO SUR", RutaLogoCompleta = ruta };
                using var form = new FrmLogin(new FalsoServicioAutenticacion(), parametros);

                form.CargarEncabezadoParaTestAsync().GetAwaiter().GetResult();

                Assert.Equal("MATADERO SUR", form.lblEmpresa.Text);
                Assert.Null(form.picLogo.Image);
            }
            finally
            {
                File.Delete(ruta);
            }
        });
    }

    [Theory]
    [InlineData("NOMBRE DE EMPRESA")] // valor sembrado sin configurar
    [InlineData("nombre de empresa")]
    [InlineData("   ")]
    public void FrmLogin_Encabezado_NombreSinConfigurarOVacio_MuestraElTituloGenerico(string nombre)
    {
        HiloSta.Ejecutar(() =>
        {
            var parametros = new ServicioParametrosFalso();
            parametros.Parametros = parametros.Parametros with { NombreEmpresa = nombre };
            using var form = new FrmLogin(new FalsoServicioAutenticacion(), parametros);

            form.CargarEncabezadoParaTestAsync().GetAwaiter().GetResult();

            Assert.Equal("Capataz", form.lblEmpresa.Text);
        });
    }

    [Fact]
    public void FrmLogin_Encabezado_ErrorDelServicio_NoImpideIniciarSesion()
    {
        HiloSta.Ejecutar(() =>
        {
            var parametros = new ServicioParametrosFalso { ExcepcionIdentidad = new InvalidOperationException("base de datos no disponible") };
            var auth = new FalsoServicioAutenticacion
            {
                ResultadoARetornar = new ResultadoLogin(ResultadoLoginTipo.Exitoso, "OK", DebeCambiarPassword: false)
            };
            using var form = new FrmLogin(auth, parametros);
            form.Show();

            form.CargarEncabezadoParaTestAsync().GetAwaiter().GetResult(); // no lanza: solo se registra en el log
            Assert.Equal("Capataz", form.lblEmpresa.Text);

            form.txtUsuario.Text = "admin";
            form.txtPassword.Text = "admin123";
            form.IngresarAsync().GetAwaiter().GetResult();

            Assert.Equal(DialogResult.OK, form.DialogResult);
        });
    }

    [Fact]
    public void FrmLogin_Encabezado_CargaLenta_NoBloqueaElInicioDeSesion()
    {
        HiloSta.Ejecutar(() =>
        {
            var pendiente = new TaskCompletionSource<PesajeFaeneo.Application.Parametros.Dtos.IdentidadEmpresa>();
            var parametros = new ServicioParametrosFalso { IdentidadPendiente = pendiente };
            var auth = new FalsoServicioAutenticacion
            {
                ResultadoARetornar = new ResultadoLogin(ResultadoLoginTipo.Exitoso, "OK", DebeCambiarPassword: false)
            };
            using var form = new FrmLogin(auth, parametros);
            form.Show();
            System.Windows.Forms.Application.DoEvents(); // Shown: la carga del encabezado queda pendiente

            form.txtUsuario.Text = "admin";
            form.txtPassword.Text = "admin123";
            form.IngresarAsync().GetAwaiter().GetResult(); // el login no espera al encabezado

            Assert.Equal(DialogResult.OK, form.DialogResult);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FrmLogin_Layout_ConEncabezadoLargoSinRecortesA96DpiYConEscalado(bool conLogo)
    {
        HiloSta.Ejecutar(() =>
        {
            var ruta = conLogo ? CrearLogoTemporal() : null;
            try
            {
                var parametros = new ServicioParametrosFalso();
                parametros.Parametros = parametros.Parametros with
                {
                    NombreEmpresa = "FRIGORIFICO INDUSTRIAL CENTRAL DE SANTA CRUZ S.R.L.",
                    RutaLogoCompleta = ruta
                };
                using var form = new FrmLogin(new FalsoServicioAutenticacion(), parametros);
                form.Show();
                form.CargarEncabezadoParaTestAsync().GetAwaiter().GetResult();
                System.Windows.Forms.Application.DoEvents();

                VerificadorLayout.Verificar(form);
                VerificadorLayout.VerificarEscalado(form);
            }
            finally
            {
                if (ruta is not null)
                {
                    File.Delete(ruta);
                }
            }
        });
    }
}
