using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.PuestaEnMarcha;
using PesajeFaeneo.Application.PuestaEnMarcha.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.WinForms.Formularios;
using PesajeFaeneo.WinForms.Formularios.PuestaEnMarcha;
using PesajeFaeneo.WinForms.Formularios.Seguridad;

namespace PesajeFaeneo.WinForms.Infraestructura;

public sealed class ContextoAplicacion : ApplicationContext
{
    private readonly IFormFactory _formFactory;
    private readonly IServicioAutenticacion _autenticacion;
    private readonly ISesionActual _sesionActual;
    private readonly IServicioPuestaEnMarcha _puestaEnMarcha;
    private readonly ILogger<ContextoAplicacion> _logger;

    public bool ReinicioSolicitado { get; private set; }

    public ContextoAplicacion(
        IFormFactory formFactory,
        IServicioAutenticacion autenticacion,
        ISesionActual sesionActual,
        IServicioPuestaEnMarcha puestaEnMarcha,
        ILogger<ContextoAplicacion> logger)
    {
        _formFactory = formFactory ?? throw new ArgumentNullException(nameof(formFactory));
        _autenticacion = autenticacion ?? throw new ArgumentNullException(nameof(autenticacion));
        _sesionActual = sesionActual ?? throw new ArgumentNullException(nameof(sesionActual));
        _puestaEnMarcha = puestaEnMarcha ?? throw new ArgumentNullException(nameof(puestaEnMarcha));
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

            if (await DebeMostrarAsistenteAsync())
            {
                bool reinicio;
                using (var asistente = _formFactory.Crear<FrmAsistentePuestaEnMarcha>())
                {
                    asistente.ShowDialog();
                    reinicio = asistente.ReinicioSolicitado;
                }

                if (reinicio)
                {
                    // Se programó una restauración desde el asistente: mismo camino que el shell (Program relanza la aplicación).
                    await _autenticacion.CerrarSesionAsync();
                    ReinicioSolicitado = true;
                    ExitThread();
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

    private async Task<bool> DebeMostrarAsistenteAsync()
    {
        var tienePermiso = _sesionActual.TienePermiso(Permiso.GestionarParametros);
        if (!tienePermiso)
        {
            return false;
        }

        try
        {
            return DebeMostrarAsistente(tienePermiso, await _puestaEnMarcha.ObtenerEstadoAsync());
        }
        catch (Exception ex)
        {
            // Nunca bloquear el inicio de sesión por el asistente.
            _logger.LogWarning(ex, "No se pudo consultar el estado de la puesta en marcha; se omite el asistente.");
            return false;
        }
    }

    internal static bool DebeMostrarAsistente(bool tienePermiso, EstadoPuestaEnMarcha? estado) =>
        tienePermiso && estado is { Completa: false };

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
