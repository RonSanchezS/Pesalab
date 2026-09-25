using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Application.Balanza;

public class ServicioConfiguracionBalanza : IServicioConfiguracionBalanza
{
    private readonly IUnidadDeTrabajoFactory _uowFactory;
    private readonly ISesionActual _sesionActual;
    private readonly IServicioBalanza _servicioBalanza;
    private readonly IEnumeradorPuertosSerie _enumeradorPuertos;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ServicioConfiguracionBalanza> _logger;

    public ServicioConfiguracionBalanza(
        IUnidadDeTrabajoFactory uowFactory,
        ISesionActual sesionActual,
        IServicioBalanza servicioBalanza,
        IEnumeradorPuertosSerie enumeradorPuertos,
        TimeProvider timeProvider,
        ILogger<ServicioConfiguracionBalanza> logger)
    {
        _uowFactory = uowFactory ?? throw new ArgumentNullException(nameof(uowFactory));
        _sesionActual = sesionActual ?? throw new ArgumentNullException(nameof(sesionActual));
        _servicioBalanza = servicioBalanza ?? throw new ArgumentNullException(nameof(servicioBalanza));
        _enumeradorPuertos = enumeradorPuertos ?? throw new ArgumentNullException(nameof(enumeradorPuertos));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ConfiguracionBalanzaLeida> ObtenerAsync(CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirPermiso(Permiso.ConfigurarBalanza);

        await using var uow = await _uowFactory.CrearAsync(cancellationToken).ConfigureAwait(false);
        var e = await uow.ConfiguracionBalanza.ObtenerAsync(cancellationToken).ConfigureAwait(false);

        return new ConfiguracionBalanzaLeida(MapeoBalanza.ADto(e), e.ModificadoEn);
    }

    public IReadOnlyList<string> ListarPuertosSerie()
    {
        _sesionActual.ExigirPermiso(Permiso.ConfigurarBalanza);
        return _enumeradorPuertos.Listar();
    }

    public async Task<bool> ActualizarAsync(ConfiguracionBalanzaDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        _sesionActual.ExigirPermiso(Permiso.ConfigurarBalanza);
        var sesion = _sesionActual.ExigirSesion();

        var normalizado = ValidadorConfiguracionBalanza.Normalizar(dto);
        var errores = ValidadorConfiguracionBalanza.Validar(normalizado);
        if (errores.Count > 0)
        {
            throw new ValidacionException(errores);
        }

        string nombreUsuario = sesion.NombreUsuario;
        string descripcionNueva = DescripcionConexion.Crear(normalizado);

        bool huboCambios;
        await using (var uow = await _uowFactory.CrearAsync(cancellationToken).ConfigureAwait(false))
        {
            var e = await uow.ConfiguracionBalanza.ObtenerAsync(cancellationToken).ConfigureAwait(false);
            var antes = FabricaAuditoria.CrearSnapshot(e);

            MapeoBalanza.Aplicar(normalizado, e);
            var despues = FabricaAuditoria.CrearSnapshot(e);

            if (antes == despues)
            {
                return false;
            }

            e.ModificadoEn = _timeProvider.GetUtcNow().UtcDateTime;
            var auditoria = FabricaAuditoria.Crear(
                _timeProvider,
                sesion.Id,
                AccionAuditoria.CambioConfigBalanza,
                EntidadesAuditoria.ConfiguracionBalanza,
                "1",
                antes: antes,
                despues: despues,
                motivo: "Actualización de configuración de balanza");

            uow.Auditoria.Registrar(auditoria);
            await uow.GuardarCambiosAsync(cancellationToken).ConfigureAwait(false);
            huboCambios = true;
        }

        _logger.LogInformation(
            "Configuración de balanza actualizada por {Usuario}: {Descripcion}",
            nombreUsuario,
            descripcionNueva);

        try
        {
            await _servicioBalanza.ReiniciarAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo reiniciar el servicio de balanza tras actualizar la configuración.");
        }

        return huboCambios;
    }
}
