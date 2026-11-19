#if CAPTURAS_MANUAL
using PesajeFaeneo.Infrastructure.Balanza.Demo;
using PesajeFaeneo.WinForms.Formularios;
using PesajeFaeneo.WinForms.Formularios.Ayuda;
using PesajeFaeneo.WinForms.Formularios.Seguridad;

namespace PesajeFaeneo.WinForms.Capturas;

internal sealed partial class GuionCapturas
{
    /// <summary>Pantallas previas a iniciar sesión: login vacío, con error y cambio de contraseña obligatorio.</summary>
    private async Task EscenasComunAntesDeSesionAsync()
    {
        await Escena("comun-01", async () =>
        {
            await CerrarSesionSiHayAsync();
            var f = await AbrirAsync<FrmLogin>(esperar: false);
            await Task.Delay(1500); // el encabezado (logo y nombre) se carga sin EjecutarAsync
            f.ActiveControl = f.txtUsuario;
            Foto("comun-01-login", "Pantalla de inicio de sesión",
                "Pantalla de inicio de sesión con el logo y el nombre de la empresa, vacía y lista para escribir el usuario.",
                f, f.txtUsuario, f.txtPassword, f.btnIngresar, f.btnSalir);
        });

        await Escena("comun-02", async () =>
        {
            var f = await AbrirAsync<FrmLogin>(esperar: false);
            await Task.Delay(1500);
            f.txtUsuario.Text = _cfg.Operador.NombreUsuario;
            f.txtPassword.Text = "clave-equivocada";
            await f.IngresarAsync();
            f.ActiveControl = f.txtUsuario;
            await Task.Delay(300);
            Foto("comun-02-login-error", "Inicio de sesión con datos incorrectos",
                "Mensaje en rojo «Usuario o contraseña incorrectos.» tras un intento con una contraseña equivocada.",
                f, f.txtUsuario, f.txtPassword, f.lblMensaje, f.btnIngresar);
        });

        await Escena("comun-03", async () =>
        {
            var f = await AbrirAsync<FrmCambioPassword>(c => c.Obligatorio = true);
            Invocar(f, "SetCampos", "••••••••", "", "");
            f.Refresh();
            Foto("comun-03-cambio-password-obligatorio", "Cambio de contraseña obligatorio",
                "Ventana que aparece al ingresar por primera vez o tras un restablecimiento: «Debe cambiar su contraseña para continuar».",
                f, Campo<TextBox>(f, "txtActual"), Campo<TextBox>(f, "txtNueva"), Campo<TextBox>(f, "txtConfirmacion"), Campo<Button>(f, "btnAceptar"), Campo<Button>(f, "btnCancelar"));
        });
    }

}
#endif
