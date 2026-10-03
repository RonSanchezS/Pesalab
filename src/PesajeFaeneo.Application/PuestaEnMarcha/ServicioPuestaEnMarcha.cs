using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.PuestaEnMarcha.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Application.PuestaEnMarcha;

/// <summary>
/// Estado y cierre del asistente de puesta en marcha (D13-D16). Corre con sesión: exige GestionarParametros en cada método.
/// </summary>
public class ServicioPuestaEnMarcha : IServicioPuestaEnMarcha
{
    private readonly IUnidadDeTrabajoFactory _uowFactory;
    private readonly ISesionActual _sesionActual;
    private readonly IAlmacenConfiguracionImpresion _almacenImpresion;
    private readonly TimeProvider _tiempo;
    private readonly ILogger<ServicioPuestaEnMarcha> _logger;

    public ServicioPuestaEnMarcha(
        IUnidadDeTrabajoFactory uowFactory,
        ISesionActual sesionActual,
        IAlmacenConfiguracionImpresion almacenImpresion,
        TimeProvider tiempo,
        ILogger<ServicioPuestaEnMarcha> logger)
    {
        _uowFactory = uowFactory ?? throw new ArgumentNullException(nameof(uowFactory));
        _sesionActual = sesionActual ?? throw new ArgumentNullException(nameof(sesionActual));
        _almacenImpresion = almacenImpresion ?? throw new ArgumentNullException(nameof(almacenImpresion));
        _tiempo = tiempo ?? throw new ArgumentNullException(nameof(tiempo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EstadoPuestaEnMarcha> ObtenerEstadoAsync(CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirPermiso(Permiso.GestionarParametros);

        bool completa;
        string nombreEmpresa;
        TipoConexion tipo;
        int fleteros;
        int usuarios;

        await using (var uow = await _uowFactory.CrearAsync(cancellationToken).ConfigureAwait(false))
        {
            var p = await uow.Parametros.ObtenerAsync(cancellationToken).ConfigureAwait(false);
            var b = await uow.ConfiguracionBalanza.ObtenerAsync(cancellationToken).ConfigureAwait(false);
            fleteros = await uow.Fleteros.ContarActivosAsync(cancellationToken).ConfigureAwait(false);
            var lista = await uow.Usuarios.ListarAsync(incluirInactivos: false, cancellationToken).ConfigureAwait(false);

            completa = p.ConfiguracionInicialCompleta;
            nombreEmpresa = p.NombreEmpresa;
            tipo = b.TipoConexion;
            usuarios = lista.Count(u => u.Rol != Rol.Administrador);
        }

        var impresion = await _almacenImpresion.LeerAsync(cancellationToken).ConfigureAwait(false) is not null;

        return new EstadoPuestaEnMarcha(
            completa,
            ReglasPuestaEnMarcha.EmpresaConfigurada(nombreEmpresa),
            nombreEmpresa,
            tipo,
            impresion,
            fleteros,
            usuarios);
    }

    public async Task MarcarCompletaAsync(bool confirmaSimulador, CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirPermiso(Permiso.GestionarParametros);
        var sesion = _sesionActual.ExigirSesion();

        await using var uow = await _uowFactory.CrearAsync(cancellationToken).ConfigureAwait(false);
        var p = await uow.Parametros.ObtenerAsync(cancellationToken).ConfigureAwait(false);
        var b = await uow.ConfiguracionBalanza.ObtenerAsync(cancellationToken).ConfigureAwait(false);

        if (p.ConfiguracionInicialCompleta)
        {
            return;
        }

        if (!ReglasPuestaEnMarcha.EmpresaConfigurada(p.NombreEmpresa))
        {
            throw new ValidacionException(CamposPuestaEnMarcha.Empresa, "Configure los datos de la empresa antes de finalizar.");
        }

        var conSimulador = b.TipoConexion == TipoConexion.Simulador;
        if (conSimulador && !confirmaSimulador)
        {
            throw new ValidacionException(
                CamposPuestaEnMarcha.Balanza,
                "La balanza está en modo Simulador. Configure la balanza real o confirme que continuará con el simulador (solo pruebas).");
        }

        p.ConfiguracionInicialCompleta = true;
        p.ModificadoEn = _tiempo.GetUtcNow().UtcDateTime;

        uow.Auditoria.Registrar(FabricaAuditoria.Crear(
            _tiempo,
            sesion.Id,
            AccionAuditoria.CambioParametros,
            EntidadesAuditoria.ParametrosSistema,
            "1",
            antes: new { ConfiguracionInicialCompleta = false },
            despues: new { ConfiguracionInicialCompleta = true },
            motivo: conSimulador ? "Puesta en marcha completada (balanza en modo simulador)" : "Puesta en marcha completada"));

        await uow.GuardarCambiosAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Puesta en marcha completada por {Usuario} (balanza en simulador: {Simulador}).",
            sesion.NombreUsuario,
            conSimulador);
    }
}
