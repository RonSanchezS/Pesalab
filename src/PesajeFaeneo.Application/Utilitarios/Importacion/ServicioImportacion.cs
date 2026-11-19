using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Licencias;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Application.Utilitarios.Backup;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Application.Utilitarios.Importacion;

public sealed class ServicioImportacion : IServicioImportacion
{
    private readonly ILectorOrigenDatos _lector;
    private readonly IRepositorioImportacion _repositorio;
    private readonly IMotorBackup _motor;
    private readonly ISesionActual _sesion;
    private readonly IServicioLicencia _licencia;
    private readonly TimeProvider _tiempo;
    private readonly ILogger<ServicioImportacion> _logger;

    public ServicioImportacion(
        ILectorOrigenDatos lector,
        IRepositorioImportacion repositorio,
        IMotorBackup motor,
        ISesionActual sesion,
        IServicioLicencia licencia,
        TimeProvider tiempo,
        ILogger<ServicioImportacion> logger)
    {
        _lector = lector ?? throw new ArgumentNullException(nameof(lector));
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _motor = motor ?? throw new ArgumentNullException(nameof(motor));
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        _licencia = licencia ?? throw new ArgumentNullException(nameof(licencia));
        _tiempo = tiempo ?? throw new ArgumentNullException(nameof(tiempo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<string>> ListarTablasAccessAsync(string ruta, CancellationToken cancellationToken = default)
    {
        _sesion.ExigirPermiso(Permiso.ImportarLegado);
        return await _lector.ListarTablasAsync(ruta, cancellationToken).ConfigureAwait(false);
    }

    public async Task<VistaOrigen> LeerOrigenAsync(SolicitudLectura lectura, TipoImportacion tipo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lectura);
        _sesion.ExigirPermiso(Permiso.ImportarLegado);

        var tabla = await _lector.LeerAsync(lectura, cancellationToken).ConfigureAwait(false);
        var muestra = tabla.Filas.Take(ReglasImportacion.FilasVistaPrevia).ToList();
        var mapeoSugerido = SugeridorMapeo.Sugerir(tipo, tabla.Columnas);

        return new VistaOrigen(tabla.Columnas, muestra, tabla.Filas.Count, tabla.Codificacion, tabla.Advertencias, mapeoSugerido);
    }

    public async Task<ResultadoImportacion> SimularAsync(SolicitudImportacion solicitud, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        _sesion.ExigirPermiso(Permiso.ImportarLegado);
        var sesion = _sesion.ExigirSesion();

        // F13 (D4): importar y simular se bloquean en modo consulta, antes de leer el origen y de abrir la transacción.
        _licencia.ExigirOperacion();

        var (tabla, plan, huella) = await PrepararInternoAsync(solicitud, cancellationToken).ConfigureAwait(false);
        var ahora = _tiempo.GetUtcNow().UtcDateTime;

        int insertadas = 0;
        if (plan.FilasAImportar > 0)
        {
            insertadas = await _repositorio.EscribirAsync(plan, sesion.Id, ahora, auditoria: null, confirmar: false, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Simulacro de importación de {Tipo} ({Formato}): {FilasLeidas} leídas, {FilasAImportar} a importar, {ConError} con error por usuario {UsuarioId}.",
            solicitud.Tipo, solicitud.Lectura.Formato, plan.FilasLeidas, plan.FilasAImportar, plan.FilasConError, sesion.Id);

        return new ResultadoImportacion(
            EsSimulacro: true,
            Huella: huella,
            Tipo: plan.Tipo,
            FilasLeidas: plan.FilasLeidas,
            FilasAImportar: plan.FilasAImportar,
            Insertadas: insertadas,
            FilasOmitidas: plan.FilasOmitidas,
            FilasConError: plan.FilasConError,
            FilasConAdvertencia: plan.FilasConAdvertencia,
            Incidencias: plan.Incidencias,
            IncidenciasTruncadas: plan.IncidenciasTruncadas,
            BackupPrevio: null);
    }

    public async Task<ResultadoImportacion> ImportarAsync(SolicitudImportacion solicitud, string huellaSimulacro, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        _sesion.ExigirPermiso(Permiso.ImportarLegado);
        var sesion = _sesion.ExigirSesion();

        // F13 (D4): ver SimularAsync.
        _licencia.ExigirOperacion();

        var (tabla, plan, huella) = await PrepararInternoAsync(solicitud, cancellationToken).ConfigureAwait(false);

        if (!string.Equals(huella, huellaSimulacro, StringComparison.Ordinal))
        {
            throw new ReglaNegocioException("IMP_CAMBIO", "El archivo o el mapeo cambiaron desde el simulacro. Vuelva a ejecutar el simulacro.");
        }

        if (plan.FilasAImportar == 0)
        {
            throw new ReglaNegocioException("IMP_NADA", "No hay filas para importar.");
        }

        ResultadoCreacionBackup resguardo;
        try
        {
            resguardo = await _motor.CrearAsync(new SolicitudBackup(TipoBackup.PreImportacion, 0, null), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear la copia de seguridad previa a la importación.");
            throw new ReglaNegocioException("IMP_SIN_RESGUARDO", "No se pudo crear la copia de seguridad previa; no se importó nada.", ex);
        }

        var ahora = _tiempo.GetUtcNow().UtcDateTime;
        var auditoria = FabricaAuditoria.Crear(
            _tiempo,
            sesion.Id,
            AccionAuditoria.Importacion,
            EntidadesAuditoria.Sistema,
            solicitud.Tipo.ToString(),
            despues: new
            {
                Archivo = Path.GetFileName(solicitud.Lectura.Ruta),
                Formato = solicitud.Lectura.Formato.ToString(),
                solicitud.Lectura.Tabla,
                plan.FilasLeidas,
                AImportar = plan.FilasAImportar,
                plan.FilasOmitidas,
                plan.FilasConError,
                plan.FilasConAdvertencia,
                HuellaArchivo = tabla.Huella,
                Backup = resguardo.Backup.NombreArchivo
            });

        int insertadas = await _repositorio.EscribirAsync(plan, sesion.Id, ahora, auditoria, confirmar: true, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Importación confirmada de {Tipo} ({Formato}): {Insertadas} insertadas, {Omitidas} omitidas, {ConError} con error por usuario {UsuarioId}. Backup: {Backup}.",
            solicitud.Tipo, solicitud.Lectura.Formato, insertadas, plan.FilasOmitidas, plan.FilasConError, sesion.Id, resguardo.Backup.NombreArchivo);

        return new ResultadoImportacion(
            EsSimulacro: false,
            Huella: huella,
            Tipo: plan.Tipo,
            FilasLeidas: plan.FilasLeidas,
            FilasAImportar: plan.FilasAImportar,
            Insertadas: insertadas,
            FilasOmitidas: plan.FilasOmitidas,
            FilasConError: plan.FilasConError,
            FilasConAdvertencia: plan.FilasConAdvertencia,
            Incidencias: plan.Incidencias,
            IncidenciasTruncadas: plan.IncidenciasTruncadas,
            BackupPrevio: resguardo.Backup.NombreArchivo);
    }

    private async Task<(TablaOrigen Tabla, PlanImportacion Plan, string Huella)> PrepararInternoAsync(
        SolicitudImportacion solicitud,
        CancellationToken ct)
    {
        var tabla = await _lector.LeerAsync(solicitud.Lectura, ct).ConfigureAwait(false);
        var claves = await _repositorio.ObtenerClavesAsync(ct).ConfigureAwait(false);
        var ahoraUtc = _tiempo.GetUtcNow().UtcDateTime;
        var zona = _tiempo.LocalTimeZone;
        var plan = await Task.Run(() => PreparadorImportacion.Preparar(
            solicitud.Tipo,
            tabla,
            solicitud.Mapeo,
            claves,
            ahoraUtc,
            zona), ct).ConfigureAwait(false);
        var huella = CalcularHuella(tabla.Huella, solicitud);
        return (tabla, plan, huella);
    }

    private static string CalcularHuella(string huellaTabla, SolicitudImportacion solicitud)
    {
        var inv = CultureInfo.InvariantCulture;
        var partesMapeo = solicitud.Mapeo
            .OrderBy(k => k.Key, StringComparer.Ordinal)
            .Select(k => $"{k.Key}={k.Value?.ToString(inv) ?? ""}");
        var cadenaMapeo = string.Join(",", partesMapeo);
        var raw = $"{huellaTabla}|{solicitud.Tipo}|{solicitud.Lectura.Formato}|{solicitud.Lectura.Tabla ?? ""}|{solicitud.Lectura.PaginaCodigos?.ToString(inv) ?? ""}|{cadenaMapeo}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }
}
