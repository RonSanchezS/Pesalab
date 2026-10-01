using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.WinForms.Formularios;
using PesajeFaeneo.WinForms.Formularios.Seguridad;

namespace PesajeFaeneo.WinForms.Infraestructura;

public sealed class ContextoAplicacion : ApplicationContext
{
    private readonly IFormFactory _formFactory;
    private readonly IServicioAutenticacion _autenticacion;
    private readonly ISesionActual _sesionActual;
    private readonly ILogger<ContextoAplicacion> _logger;

    public bool ReinicioSolicitado { get; private set; }

    public ContextoAplicacion(
        IFormFactory formFactory,
        IServicioAutenticacion autenticacion,
        ISesionActual sesionActual,
        ILogger<ContextoAplicacion> logger)
    {
        _formFactory = formFactory ?? throw new ArgumentNullException(nameof(formFactory));
        _autenticacion = autenticacion ?? throw new ArgumentNullException(nameof(autenticacion));
        _sesionActual = sesionActual ?? throw new ArgumentNullException(nameof(sesionActual));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        MostrarLogin();
    }

    private void MostrarLogin()
    {
        var login = _formFactory.Crear<FrmLogin>();
        login.FormClosed += (_, _) =>
        {
            if (login.DialogResult == DialogResult.OK)
            {
                ContinuarTrasLogin(login.DebeCambiarPassword);
            }
            else
            {
                ExitThread();
            }
        };
        login.Show();
    }

    private async void ContinuarTrasLogin(bool debeCambiar)
    {
        try
        {
            if (debeCambiar)
            {
                using var frmCambio = _formFactory.Crear<FrmCambioPassword>();
                frmCambio.Obligatorio = true;
                var resultado = frmCambio.ShowDialog();
                if (resultado != DialogResult.OK)
                {
                    await _autenticacion.CerrarSesionAsync();
                    MostrarLogin();
                    return;
                }
            }

            MostrarShell();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante la transición posterior al inicio de sesión");
            ManejadorErrores.Mostrar(ex);
            ExitThread();
        }
    }

    private void MostrarShell()
    {
        var shell = _formFactory.Crear<FrmPrincipal>();
        shell.FormClosed += async (_, _) =>
        {
            try
            {
                await _autenticacion.CerrarSesionAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar auditoría de cierre de sesión al cerrar shell");
            }

            try
            {
                if (shell.Solicitud == SolicitudCierreShell.CerrarSesion)
                {
                    MostrarLogin();
                }
                else
                {
                    ReinicioSolicitado = shell.Solicitud == SolicitudCierreShell.ReiniciarAplicacion;
                    ExitThread();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar el cierre del shell");
                ManejadorErrores.Mostrar(ex);
                ExitThread();
            }
        };
        shell.Show();
    }
}
