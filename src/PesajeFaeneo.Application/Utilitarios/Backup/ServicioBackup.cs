namespace PesajeFaeneo.Application.Utilitarios.Backup;

using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;

public sealed class ServicioBackup : IServicioBackup
{
    private readonly IMotorBackup _motor;
    private readonly IAlmacenConfiguracionBackup _almacen;
    private readonly IProgramadorBackupAutomatico _programador;
    private readonly IUnidadDeTrabajoFactory _uowFactory;
    private readonly ISesionActual _sesion;
    private readonly TimeProvider _tiempo;
    private readonly ILogger<ServicioBackup> _logger;

    public ServicioBackup(
        IMotorBackup motor,
        IAlmacenConfiguracionBackup almacen,
        IProgramadorBackupAutomatico programador,
        IUnidadDeTrabajoFactory uowFactory,
        ISesionActual sesion,
        TimeProvider tiempo,
        ILogger<ServicioBackup> logger)
    {
        _motor = motor ?? throw new ArgumentNullException(nameof(motor));
        _almacen = almacen ?? throw new ArgumentNullException(nameof(almacen));
        _programador = programador ?? throw new ArgumentNullException(nameof(programador));
        _uowFactory = uowFactory ?? throw new ArgumentNullException(nameof(uowFactory));
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        _tiempo = tiempo ?? throw new ArgumentNullException(nameof(tiempo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string CarpetaBackups => _motor.CarpetaBackups;

    public ResultadoBackupAutomatico? UltimoAutomatico => _programador.UltimoResultado;

    public async Task<IReadOnlyList<InfoBackup>> ListarAsync(CancellationToken cancellationToken = default)
    {
        _sesion.ExigirPermiso(Permiso.BackupRestore);
        return await _motor.ListarAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ExportarAsync(string rutaBackup, string rutaDestino, CancellationToken cancellationToken = default)
    {
        _sesion.ExigirPermiso(Permiso.BackupRestore);
        await _motor.ExportarAsync(rutaBackup, rutaDestino, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Copia de seguridad {Origen} exportada a {Destino}.",
            Path.GetFileName(rutaBackup),
            Path.GetFileName(rutaDestino));
    }

    public async Task<ValidacionRestauracion> ValidarAsync(string rutaBackup, CancellationToken cancellationToken = default)
    {
        _sesion.ExigirPermiso(Permiso.BackupRestore);
        return await _motor.ValidarParaRestaurarAsync(rutaBackup, cancellationToken).ConfigureAwait(false);
    }

    public async Task DescartarValidacionAsync(string token, CancellationToken cancellationToken = default)
    {
        _sesion.ExigirPermiso(Permiso.BackupRestore);
        await _motor.DescartarValidacionAsync(token, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ConfiguracionBackup> ObtenerConfiguracionAsync(CancellationToken cancellationToken = default)
    {
        _sesion.ExigirPermiso(Permiso.BackupRestore);
        return await _almacen.LeerAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ResultadoCreacionBackup> CrearManualAsync(CancellationToken cancellationToken = default)
    {
        _sesion.ExigirPermiso(Permiso.BackupRestore);
        var sesion = _sesion.Usuario ?? throw new NoAutorizadoException();

        var config = await _almacen.LeerAsync(cancellationToken).ConfigureAwait(false);
        var r = await _motor.CrearAsync(
            new SolicitudBackup(TipoBackup.Manual, config.DiasRetencion, config.CarpetaSecundaria),
            cancellationToken).ConfigureAwait(false);

        // La copia ya existe: si la auditoría falla, informar un error haría creer que no se creó nada.
        try
        {
            await using var uow = await _uowFactory.CrearAsync(cancellationToken).ConfigureAwait(false);
            uow.Auditoria.Registrar(FabricaAuditoria.Crear(
                _tiempo,
                sesion.Id,
                AccionAuditoria.Backup,
                EntidadesAuditoria.Sistema,
                "Backup",
                despues: new { r.Backup.NombreArchivo, r.Backup.TamanoBytes, Tipo = "Manual" },
                motivo: "Copia de seguridad manual"));

            await uow.GuardarCambiosAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "La copia manual {Archivo} se creó, pero no se pudo registrar en la auditoría.", r.Backup.NombreArchivo);
        }

        return r;
    }

    public async Task<InfoBackup> ProgramarRestauracionAsync(string token, string confirmacion, CancellationToken cancellationToken = default)
    {
        _sesion.ExigirPermiso(Permiso.BackupRestore);
        var sesion = _sesion.Usuario ?? throw new NoAutorizadoException();

        if (!string.Equals(confirmacion?.Trim(), ReglasBackup.ConfirmacionRestauracion, StringComparison.Ordinal))
        {
            throw new ValidacionException(CamposBackup.Confirmacion, "Escriba RESTAURAR para confirmar.");
        }

        var resguardo = await _motor.ProgramarRestauracionAsync(token, sesion.NombreUsuario, cancellationToken).ConfigureAwait(false);

        // La restauración ya quedó programada: un fallo de auditoría no debe presentarse como si no lo estuviera
        // (se aplicaría sola al abrir la aplicación sin que el usuario lo sepa). La aplicada se audita al arrancar.
        try
        {
            await using var uow = await _uowFactory.CrearAsync(cancellationToken).ConfigureAwait(false);
            uow.Auditoria.Registrar(FabricaAuditoria.Crear(
                _tiempo,
                sesion.Id,
                AccionAuditoria.Restauracion,
                EntidadesAuditoria.Sistema,
                "Restauracion",
                despues: new { Resguardo = resguardo.NombreArchivo },
                motivo: "Restauración programada; se aplicará al reiniciar"));

            await uow.GuardarCambiosAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "La restauración quedó programada, pero no se pudo registrar en la auditoría.");
        }

        return resguardo;
    }

    public async Task GuardarConfiguracionAsync(ConfiguracionBackup configuracion, CancellationToken cancellationToken = default)
    {
        _sesion.ExigirPermiso(Permiso.BackupRestore);
        var sesion = _sesion.Usuario ?? throw new NoAutorizadoException();

        var carpetaNorm = string.IsNullOrWhiteSpace(configuracion.CarpetaSecundaria)
            ? null
            : configuracion.CarpetaSecundaria.Trim();

        var normalizada = configuracion with { CarpetaSecundaria = carpetaNorm };

        var errores = ReglasBackup.ValidarConfiguracion(normalizada);
        if (errores.Count > 0)
        {
            throw new ValidacionException(errores);
        }

        if (normalizada.CarpetaSecundaria is not null)
        {
            await _motor.VerificarCarpetaEscribibleAsync(normalizada.CarpetaSecundaria, cancellationToken).ConfigureAwait(false);
        }

        var anterior = await _almacen.LeerAsync(cancellationToken).ConfigureAwait(false);
        if (anterior == normalizada)
        {
            return;
        }

        await _almacen.GuardarAsync(normalizada, cancellationToken).ConfigureAwait(false);

        await using (var uow = await _uowFactory.CrearAsync(cancellationToken).ConfigureAwait(false))
        {
            uow.Auditoria.Registrar(FabricaAuditoria.Crear(
                _tiempo,
                sesion.Id,
                AccionAuditoria.CambioParametros,
                EntidadesAuditoria.Sistema,
                "ConfiguracionBackup",
                antes: anterior,
                despues: normalizada,
                motivo: "Cambio de configuración de copias de seguridad"));

            await uow.GuardarCambiosAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
