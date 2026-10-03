namespace PesajeFaeneo.Application.Arranque;

using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Application.Arranque.Dtos;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Utilitarios.Backup;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;

/// <summary>
/// Corre sin sesión, lo invoca Program antes de sembrar.
/// </summary>
public sealed class ActualizadorBaseDatos : IActualizadorBaseDatos
{
    private static readonly string VersionAplicacion = typeof(ActualizadorBaseDatos).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    internal const string MensajeVersionMasNueva =
        "La base de datos fue actualizada por una versión más nueva del sistema. Instale esa versión (o una posterior) para seguir trabajando. No se modificó ningún dato.";

    internal static string MensajeBackupFallido(Exception ex)
    {
        var detalle = ex is DomainException ? ex.Message : "error inesperado (ver el log).";
        return $"No se pudo crear la copia de seguridad previa a la actualización de la base de datos: {detalle} La base de datos no se modificó. Libere espacio en el disco o contacte a soporte.";
    }

    internal static string MensajeMigracionFallida(string? archivo) =>
        $"La actualización de la base de datos falló. Sus datos quedaron resguardados en la copia {archivo} (carpeta backups\\seguridad). No use el sistema: contacte a soporte para restaurarla.";

    private readonly IDatabaseInitializer _inicializador;
    private readonly IMotorBackup _motorBackup;
    private readonly IUnidadDeTrabajoFactory _uowFactory;
    private readonly TimeProvider _tiempo;
    private readonly ILogger<ActualizadorBaseDatos> _logger;

    public ActualizadorBaseDatos(
        IDatabaseInitializer inicializador,
        IMotorBackup motorBackup,
        IUnidadDeTrabajoFactory uowFactory,
        TimeProvider tiempo,
        ILogger<ActualizadorBaseDatos> logger)
    {
        _inicializador = inicializador ?? throw new ArgumentNullException(nameof(inicializador));
        _motorBackup = motorBackup ?? throw new ArgumentNullException(nameof(motorBackup));
        _uowFactory = uowFactory ?? throw new ArgumentNullException(nameof(uowFactory));
        _tiempo = tiempo ?? throw new ArgumentNullException(nameof(tiempo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ResultadoActualizacionBaseDatos> PrepararAsync(CancellationToken cancellationToken = default)
    {
        var estado = await _inicializador.EvaluarAsync(cancellationToken).ConfigureAwait(false);

        if (estado.Desconocidas.Count > 0)
        {
            _logger.LogError("La base de datos tiene migraciones desconocidas para la versión {Version}: {Migraciones}.", VersionAplicacion, string.Join(", ", estado.Desconocidas));
            throw new ArranqueBaseDatosException(MotivoFalloArranque.VersionMasNueva, MensajeVersionMasNueva);
        }

        var esActualizacion = estado.Aplicadas.Count > 0 && estado.Pendientes.Count > 0;
        string? archivo = null;

        if (esActualizacion)
        {
            _logger.LogInformation("Actualización de base de datos: {Cantidad} migraciones pendientes. Creando copia previa.", estado.Pendientes.Count);
            try
            {
                var r = await _motorBackup.CrearAsync(new SolicitudBackup(TipoBackup.PreMigracion, 0, null), cancellationToken).ConfigureAwait(false);
                archivo = r.Backup.NombreArchivo;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló la copia de seguridad previa a la actualización.");
                throw new ArranqueBaseDatosException(MotivoFalloArranque.BackupFallido, MensajeBackupFallido(ex), ex);
            }
        }

        try
        {
            await _inicializador.InicializarAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (esActualizacion)
        {
            _logger.LogError(ex, "Falló la migración de actualización de la base de datos.");
            throw new ArranqueBaseDatosException(MotivoFalloArranque.MigracionFallida, MensajeMigracionFallida(archivo), ex);
        }

        if (esActualizacion)
        {
            _logger.LogInformation("Base de datos actualizada a la versión {Version} ({Cantidad} migraciones; copia previa {Archivo}).", VersionAplicacion, estado.Pendientes.Count, archivo);
        }

        var estadoResultado = estado.Aplicadas.Count == 0
            ? EstadoActualizacionBaseDatos.NuevaInstalacion
            : esActualizacion
                ? EstadoActualizacionBaseDatos.Actualizada
                : EstadoActualizacionBaseDatos.AlDia;

        var migraciones = esActualizacion ? estado.Pendientes : (IReadOnlyList<string>)Array.Empty<string>();

        return new ResultadoActualizacionBaseDatos(estadoResultado, migraciones, archivo);
    }

    public async Task RegistrarAuditoriaAsync(ResultadoActualizacionBaseDatos resultado, CancellationToken cancellationToken = default)
    {
        if (resultado.Estado != EstadoActualizacionBaseDatos.Actualizada)
        {
            return;
        }

        await using var uow = await _uowFactory.CrearAsync(cancellationToken).ConfigureAwait(false);
        uow.Auditoria.Registrar(FabricaAuditoria.Crear(
            _tiempo,
            usuarioId: null,
            AccionAuditoria.ActualizacionSistema,
            EntidadesAuditoria.Sistema,
            "Actualizacion",
            despues: new
            {
                Version = VersionAplicacion,
                Migraciones = resultado.MigracionesAplicadas,
                Backup = resultado.ArchivoBackup
            },
            motivo: $"Base de datos actualizada a la versión {VersionAplicacion}"));

        await uow.GuardarCambiosAsync(cancellationToken).ConfigureAwait(false);
    }
}
