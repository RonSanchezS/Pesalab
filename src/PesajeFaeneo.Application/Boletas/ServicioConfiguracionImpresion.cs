using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Application.Boletas;

public class ServicioConfiguracionImpresion : IServicioConfiguracionImpresion
{
    private readonly IAlmacenConfiguracionImpresion _almacen;
    private readonly IUnidadDeTrabajoFactory _uowFactory;
    private readonly ISesionActual _sesion;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ServicioConfiguracionImpresion> _logger;

    public ServicioConfiguracionImpresion(
        IAlmacenConfiguracionImpresion almacen,
        IUnidadDeTrabajoFactory uowFactory,
        ISesionActual sesion,
        TimeProvider timeProvider,
        ILogger<ServicioConfiguracionImpresion> logger)
    {
        _almacen = almacen ?? throw new ArgumentNullException(nameof(almacen));
        _uowFactory = uowFactory ?? throw new ArgumentNullException(nameof(uowFactory));
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ConfiguracionImpresionEquipo> ObtenerAsync(CancellationToken cancellationToken = default)
    {
        _sesion.ExigirSesion();

        var leida = await _almacen.LeerAsync(cancellationToken);
        return new ConfiguracionImpresionEquipo(
            leida ?? ReglasImpresion.PorDefecto,
            leida is not null,
            _almacen.NombreEquipo);
    }

    public async Task GuardarAsync(ConfiguracionImpresion configuracion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuracion);

        _sesion.ExigirPermiso(Permiso.GestionarParametros);
        var sesion = _sesion.ExigirSesion();

        var (normalizada, errores) = ValidadorConfiguracionImpresion.Validar(configuracion);
        if (errores.Count > 0)
        {
            throw new ValidacionException(errores);
        }

        var antes = await _almacen.LeerAsync(cancellationToken);

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        await using var tx = await uow.IniciarTransaccionAsync(cancellationToken);

        var equipo = _almacen.NombreEquipo;
        var entrada = FabricaAuditoria.Crear(
            _timeProvider,
            sesion.Id,
            AccionAuditoria.CambioConfigImpresion,
            EntidadesAuditoria.ConfiguracionImpresion,
            equipo,
            antes: antes is null ? null : FabricaAuditoria.CrearSnapshot(antes, equipo),
            despues: FabricaAuditoria.CrearSnapshot(normalizada, equipo),
            motivo: $"Configuración de impresión del equipo {equipo}");

        uow.Auditoria.Registrar(entrada);
        await uow.GuardarCambiosAsync(cancellationToken);

        await _almacen.GuardarAsync(normalizada, cancellationToken);
        await tx.ConfirmarAsync(cancellationToken);

        _logger.LogInformation(
            "Configuración de impresión actualizada para el equipo {Equipo} por {Usuario}",
            equipo,
            sesion.NombreUsuario);
    }
}
