namespace PesajeFaeneo.Infrastructure.Utilitarios.Backup;

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Application.Utilitarios.Backup;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Infrastructure.Common;
using PesajeFaeneo.Infrastructure.Persistencia;

public sealed class SqliteBackupService : IMotorBackup
{
    private readonly AppPaths _appPaths;
    private readonly IDbContextFactory<PesajeDbContext> _contextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SqliteBackupService> _logger;
    private readonly string _nombreEquipo;
    private readonly SemaphoreSlim _bloqueo = new(1, 1);
    private static readonly string VersionAplicacion = typeof(SqliteBackupService).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    private readonly Lazy<IReadOnlyList<string>> _migracionesConocidas;

    internal Func<string, long>? EspacioLibreParaTest { get; set; }

    public string CarpetaBackups => _appPaths.BackupsDir;

    public SqliteBackupService(
        AppPaths appPaths,
        IDbContextFactory<PesajeDbContext> contextFactory,
        TimeProvider timeProvider,
        ILogger<SqliteBackupService> logger,
        string nombreEquipo)
    {
        _appPaths = appPaths ?? throw new ArgumentNullException(nameof(appPaths));
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _nombreEquipo = string.IsNullOrWhiteSpace(nombreEquipo) ? Environment.MachineName : nombreEquipo;

        _migracionesConocidas = new Lazy<IReadOnlyList<string>>(() =>
        {
            using var ctx = _contextFactory.CreateDbContext();
            return ctx.Database.GetMigrations().ToList();
        });
    }

    public async Task<ResultadoCreacionBackup> CrearAsync(SolicitudBackup solicitud, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        await _bloqueo.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => CrearInternoAsync(solicitud, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _bloqueo.Release();
        }
    }

    private ResultadoCreacionBackup CrearInternoAsync(SolicitudBackup solicitud, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var ahoraUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var local = TimeZoneInfo.ConvertTimeFromUtc(ahoraUtc, _timeProvider.LocalTimeZone);

        // 2. Espacio
        var fiDb = new FileInfo(_appPaths.DatabasePath);
        long tamanoDb = fiDb.Exists ? fiDb.Length : 0;
        var fiWal = new FileInfo(_appPaths.DatabasePath + "-wal");
        long tamanoWal = fiWal.Exists ? fiWal.Length : 0;
        long necesario = (2 * (tamanoDb + tamanoWal)) + (50L * 1024 * 1024);
        long libre = EspacioLibreParaTest?.Invoke(_appPaths.BackupsDir) ?? new DriveInfo(Path.GetPathRoot(_appPaths.BackupsDir)!).AvailableFreeSpace;

        if (libre < necesario)
        {
            throw new ReglaNegocioException("BAK_ESPACIO", "No hay espacio suficiente en el disco para crear la copia de seguridad.");
        }

        // 3. Temporal
        var tmp = Path.Combine(_appPaths.BackupsDir, $".tmp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmp);

        try
        {
            // 4. Copia consistente
            var rutaCopia = Path.Combine(tmp, "pesaje.db");
            var cadenaOrigen = new SqliteConnectionStringBuilder
            {
                DataSource = _appPaths.DatabasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString();

            var cadenaDestino = new SqliteConnectionStringBuilder
            {
                DataSource = rutaCopia,
                Pooling = false
            }.ToString();

            EstadisticasBaseDatos estadisticas;
            using (var origen = new SqliteConnection(cadenaOrigen))
            using (var destino = new SqliteConnection(cadenaDestino))
            {
                origen.Open();
                EjecutarNoConsulta(origen, "PRAGMA busy_timeout=5000;");
                destino.Open();
                origen.BackupDatabase(destino);
                EjecutarNoConsulta(destino, "PRAGMA journal_mode=DELETE;");
                if (!string.Equals(Escalar(destino, "PRAGMA quick_check;"), "ok", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ReglaNegocioException("BAK_INTEGRIDAD", "La base de datos actual tiene errores de integridad; no se creó la copia. Revise el log.");
                }
                estadisticas = LeerEstadisticas(destino);
            }

            // 5. SHA-256 de la copia
            string sha256;
            long tamanoCopia;
            using (var fs = new FileStream(rutaCopia, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                tamanoCopia = fs.Length;
                sha256 = Convert.ToHexString(SHA256.HashData(fs));
            }

            // 6. Logos
            var rutasLogo = new List<string>();
            if (Directory.Exists(_appPaths.LogoDir))
            {
                var archivos = Directory.GetFiles(_appPaths.LogoDir, "*", SearchOption.TopDirectoryOnly)
                    .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var f in archivos)
                {
                    var ext = Path.GetExtension(f);
                    var fi = new FileInfo(f);
                    if (ReglasBackup.ExtensionesLogo.Contains(ext) && fi.Length <= ReglasBackup.TamanoMaximoLogo)
                    {
                        if (rutasLogo.Count < ReglasBackup.MaximoArchivosLogo)
                        {
                            rutasLogo.Add(f);
                        }
                        else
                        {
                            _logger.LogWarning("Se superó el límite de logos en backup: se ignora el archivo {Archivo}.", Path.GetFileName(f));
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Archivo de logo no admitido para backup: {Archivo}.", Path.GetFileName(f));
                    }
                }
            }

            // 7. Manifiesto y zip
            var manifiesto = new ManifiestoBackup(
                ReglasBackup.FormatoManifiesto,
                ReglasBackup.NombreAplicacion,
                VersionAplicacion,
                _nombreEquipo,
                solicitud.Tipo,
                ahoraUtc,
                estadisticas.UltimaMigracion,
                estadisticas.Migraciones,
                sha256,
                tamanoCopia,
                estadisticas.Pesajes,
                estadisticas.Ganaderos,
                estadisticas.Clientes,
                estadisticas.Usuarios,
                estadisticas.UltimoNumeroBoleta,
                rutasLogo.Select(Path.GetFileName).ToList()!);

            var tmpZip = Path.Combine(tmp, "backup.zip");
            ArchivoBackupZip.Escribir(tmpZip, rutaCopia, manifiesto, rutasLogo);

            // 8. Destino
            var carpeta = Path.Combine(_appPaths.BackupsDir, ReglasBackup.Carpeta(solicitud.Tipo));
            Directory.CreateDirectory(carpeta);
            var nombre = ReglasBackup.NombreArchivo(solicitud.Tipo, local);
            var rutaFinal = Path.Combine(carpeta, nombre);

            if (solicitud.Tipo == TipoBackup.Automatico)
            {
                File.Move(tmpZip, rutaFinal, overwrite: true);
            }
            else
            {
                if (!File.Exists(rutaFinal))
                {
                    File.Move(tmpZip, rutaFinal, overwrite: false);
                }
                else
                {
                    var baseName = Path.GetFileNameWithoutExtension(nombre);
                    var ext = Path.GetExtension(nombre);
                    var publicado = false;
                    for (int i = 2; i <= 99; i++)
                    {
                        var candidata = Path.Combine(carpeta, $"{baseName}_{i}{ext}");
                        if (!File.Exists(candidata))
                        {
                            File.Move(tmpZip, candidata, overwrite: false);
                            rutaFinal = candidata;
                            nombre = Path.GetFileName(candidata);
                            publicado = true;
                            break;
                        }
                    }

                    if (!publicado)
                    {
                        throw new ReglaNegocioException("BAK_COLISION", "Se alcanzó el límite de archivos de backup con el mismo nombre.");
                    }
                }
            }

            // 9. Retención
            if (solicitud.Tipo == TipoBackup.Automatico)
            {
                var nombres = new DirectoryInfo(carpeta).GetFiles("*.zip").Select(f => f.Name);
                var aEliminar = ReglasBackup.SeleccionarAutomaticosAEliminar(nombres, solicitud.DiasRetencion, DateOnly.FromDateTime(local));
                foreach (var ae in aEliminar)
                {
                    try
                    {
                        File.Delete(Path.Combine(carpeta, ae));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        _logger.LogWarning(ex, "No se pudo eliminar el backup automático expirado {Archivo}.", ae);
                    }
                }
            }
            else if (solicitud.Tipo == TipoBackup.PreRestauracion || solicitud.Tipo == TipoBackup.PreImportacion)
            {
                var nombres = new DirectoryInfo(carpeta).GetFiles("*.zip").Select(f => f.Name);
                var aEliminar = ReglasBackup.SeleccionarSeguridadAEliminar(nombres, solicitud.Tipo);
                foreach (var ae in aEliminar)
                {
                    try
                    {
                        File.Delete(Path.Combine(carpeta, ae));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        _logger.LogWarning(ex, "No se pudo eliminar el backup de seguridad expirado {Archivo}.", ae);
                    }
                }
            }

            // 10. Carpeta secundaria
            string? advertencia = null;
            if (!string.IsNullOrWhiteSpace(solicitud.CarpetaSecundaria) &&
                (solicitud.Tipo == TipoBackup.Manual || solicitud.Tipo == TipoBackup.Automatico))
            {
                try
                {
                    var carpeta2 = solicitud.CarpetaSecundaria.Trim();
                    Directory.CreateDirectory(carpeta2);
                    var destinoSecundario = Path.Combine(carpeta2, nombre);
                    File.Copy(rutaFinal, destinoSecundario, overwrite: true);

                    if (solicitud.Tipo == TipoBackup.Automatico)
                    {
                        var nombres2 = new DirectoryInfo(carpeta2).GetFiles("*.zip").Select(f => f.Name);
                        var aEliminar2 = ReglasBackup.SeleccionarAutomaticosAEliminar(nombres2, solicitud.DiasRetencion, DateOnly.FromDateTime(local));
                        foreach (var ae in aEliminar2)
                        {
                            try
                            {
                                File.Delete(Path.Combine(carpeta2, ae));
                            }
                            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                            {
                                _logger.LogWarning(ex, "No se pudo eliminar el backup expirado de la carpeta secundaria {Archivo}.", ae);
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
                {
                    advertencia = "La copia se creó, pero no se pudo copiar a la carpeta secundaria (¿está conectada?).";
                    _logger.LogWarning(ex, "Falló la copia a la carpeta secundaria {Carpeta}.", solicitud.CarpetaSecundaria);
                }
            }

            // 11. Resultado
            _logger.LogInformation(
                "Backup {Tipo} creado: {Archivo} ({Bytes} bytes, {Ms} ms).",
                solicitud.Tipo,
                Path.GetFileName(rutaFinal),
                new FileInfo(rutaFinal).Length,
                sw.ElapsedMilliseconds);

            return new ResultadoCreacionBackup(Info(rutaFinal, manifiesto), advertencia);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tmp))
                {
                    Directory.Delete(tmp, true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    public Task<bool> ExisteAutomaticoDelDiaAsync(DateOnly fechaLocal, CancellationToken cancellationToken = default)
    {
        var nombre = ReglasBackup.NombreArchivo(TipoBackup.Automatico, fechaLocal.ToDateTime(TimeOnly.MinValue));
        var ruta = Path.Combine(_appPaths.BackupsDir, ReglasBackup.CarpetaAutomaticos, nombre);
        return Task.FromResult(File.Exists(ruta));
    }

    public Task<IReadOnlyList<InfoBackup>> ListarAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var carpetas = new[]
            {
                Path.Combine(_appPaths.BackupsDir, ReglasBackup.CarpetaManuales),
                Path.Combine(_appPaths.BackupsDir, ReglasBackup.CarpetaAutomaticos),
                Path.Combine(_appPaths.BackupsDir, ReglasBackup.CarpetaSeguridad)
            };

            var resultado = new List<InfoBackup>();
            foreach (var c in carpetas)
            {
                if (!Directory.Exists(c)) continue;
                var zips = Directory.GetFiles(c, "*.zip", SearchOption.TopDirectoryOnly);
                foreach (var ruta in zips)
                {
                    var nombre = Path.GetFileName(ruta);
                    var fi = new FileInfo(ruta);
                    var manifiesto = ArchivoBackupZip.LeerManifiesto(ruta);
                    if (manifiesto is not null)
                    {
                        resultado.Add(Info(ruta, manifiesto));
                    }
                    else
                    {
                        var tipo = c.EndsWith(ReglasBackup.CarpetaManuales, StringComparison.OrdinalIgnoreCase) ? TipoBackup.Manual :
                                   c.EndsWith(ReglasBackup.CarpetaAutomaticos, StringComparison.OrdinalIgnoreCase) ? TipoBackup.Automatico :
                                   TipoBackup.PreRestauracion;

                        resultado.Add(new InfoBackup(
                            ruta,
                            nombre,
                            tipo,
                            fi.LastWriteTimeUtc,
                            fi.Length,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            0,
                            null,
                            Valido: false,
                            Problema: "No se pudo leer: archivo dañado o de otro sistema."));
                    }
                }
            }

            return (IReadOnlyList<InfoBackup>)resultado
                .OrderByDescending(x => x.CreadoEnUtc)
                .Take(ReglasBackup.MaximoListado)
                .ToList();
        }, cancellationToken);
    }

    public Task ExportarAsync(string rutaBackup, string rutaDestino, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (!File.Exists(rutaBackup))
            {
                throw new ValidacionException(CamposBackup.Archivo, "La copia de seguridad ya no existe.");
            }

            var tmp = rutaDestino + ".tmp";
            try
            {
                var dirDestino = Path.GetDirectoryName(rutaDestino);
                if (!string.IsNullOrEmpty(dirDestino) && !Directory.Exists(dirDestino))
                {
                    throw new DirectoryNotFoundException($"El directorio '{dirDestino}' no existe.");
                }

                File.Copy(rutaBackup, tmp, overwrite: true);
                File.Move(tmp, rutaDestino, overwrite: true);
                _logger.LogInformation(
                    "Backup exportado: {Origen} -> {Destino}",
                    Path.GetFileName(rutaBackup),
                    Path.GetFileName(rutaDestino));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
            {
                try
                {
                    if (File.Exists(tmp))
                    {
                        File.Delete(tmp);
                    }
                }
                catch
                {
                }

                throw new ReglaNegocioException(
                    "BAK_EXPORTAR",
                    "No se pudo copiar la copia de seguridad a la carpeta elegida. Verifique que el dispositivo esté conectado, tenga espacio y permita escribir.",
                    ex);
            }
        }, cancellationToken);
    }

    public Task VerificarCarpetaEscribibleAsync(string carpeta, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(carpeta) || !Directory.Exists(carpeta))
        {
            throw new ValidacionException(CamposBackup.CarpetaSecundaria, "La carpeta no existe o no está conectada.");
        }

        var fullBase = Path.GetFullPath(_appPaths.BaseDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullCarpeta = Path.GetFullPath(carpeta).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (fullCarpeta.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidacionException(CamposBackup.CarpetaSecundaria, "Elija una carpeta fuera de la carpeta de datos del sistema (por ejemplo un pendrive o una carpeta de red).");
        }

        var prueba = Path.Combine(carpeta, $".pesaje-prueba-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(prueba, "test");
            File.Delete(prueba);
        }
        catch (Exception)
        {
            try
            {
                if (File.Exists(prueba))
                {
                    File.Delete(prueba);
                }
            }
            catch
            {
            }

            throw new ValidacionException(CamposBackup.CarpetaSecundaria, "No se puede escribir en la carpeta elegida.");
        }

        return Task.CompletedTask;
    }

    private static readonly Regex RegexValidacionToken = new(@"^validacion-[0-9a-f]{32}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<ValidacionRestauracion> ValidarParaRestaurarAsync(string rutaBackup, CancellationToken cancellationToken = default)
    {
        await _bloqueo.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => ValidarParaRestaurarInternoAsync(rutaBackup, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _bloqueo.Release();
        }
    }

    private async Task<ValidacionRestauracion> ValidarParaRestaurarInternoAsync(string rutaBackup, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rutaBackup) || !File.Exists(rutaBackup))
        {
            throw new ValidacionException(CamposBackup.Archivo, "El archivo no existe.");
        }

        var fi = new FileInfo(rutaBackup);
        if (fi.Length > ReglasBackup.TamanoMaximoBackup)
        {
            throw new ValidacionException(CamposBackup.Archivo, "La copia de seguridad no es válida: es demasiado grande.");
        }

        // 2. Borrar carpetas validacion-* anteriores
        if (Directory.Exists(_appPaths.RestoreDir))
        {
            foreach (var d in Directory.GetDirectories(_appPaths.RestoreDir, "validacion-*"))
            {
                try { Directory.Delete(d, true); } catch { }
            }
        }

        // 3. Extraer a RestoreDir/token
        var token = $"validacion-{Guid.NewGuid():N}";
        Directory.CreateDirectory(_appPaths.RestoreDir);
        var carpeta = Path.Combine(_appPaths.RestoreDir, token);
        var exito = false;
        try
        {
            var manifiesto = ArchivoBackupZip.ExtraerValidado(rutaBackup, carpeta);
            File.WriteAllText(Path.Combine(carpeta, "origen.txt"), Path.GetFileName(rutaBackup), new UTF8Encoding(false));

            // 4. Abrir pesaje.db
            var rutaDb = Path.Combine(carpeta, ReglasBackup.EntradaBaseDatos);
            var connStr = new SqliteConnectionStringBuilder
            {
                DataSource = rutaDb,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString();

            int ganaderos;
            int clientes;
            int usuariosActivos;
            int administradoresActivos;
            bool requiereActualizar;

            try
            {
                using var conn = new SqliteConnection(connStr);
                conn.Open();

                var checkIntegrity = Escalar(conn, "PRAGMA integrity_check;");
                if (!string.Equals(checkIntegrity, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ValidacionException(CamposBackup.Archivo, "La copia de seguridad no es válida: la base de datos está dañada (falló la verificación de integridad).");
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA foreign_key_check;";
                    using var r = cmd.ExecuteReader();
                    if (r.Read())
                    {
                        throw new ValidacionException(CamposBackup.Archivo, "La copia de seguridad no es válida: la base de datos tiene referencias rotas.");
                    }
                }

                var tablasRequeridas = new[]
                {
                    "Usuarios", "ParametrosSistema", "Pesajes", "Ganaderos", "Clientes", "Auditorias", "__EFMigrationsHistory"
                };

                var tablasExistentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
                    using var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        tablasExistentes.Add(r.GetString(0));
                    }
                }

                if (tablasRequeridas.Any(t => !tablasExistentes.Contains(t)))
                {
                    throw new ValidacionException(CamposBackup.Archivo, "La copia de seguridad no es válida: no pertenece a este sistema.");
                }

                var historial = new List<string>();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;";
                    using var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        historial.Add(r.GetString(0));
                    }
                }

                var conocidas = _migracionesConocidas.Value;
                if (historial.Any(h => !conocidas.Contains(h)))
                {
                    throw new ReglaNegocioException("BAK_VERSION_NUEVA", "La copia fue creada con una versión más nueva del sistema. Actualice el sistema antes de restaurarla.");
                }

                requiereActualizar = historial.Count < conocidas.Count;

                int cantParametros = Convert.ToInt32(Escalar(conn, "SELECT count(*) FROM ParametrosSistema WHERE Id = 1;"));
                if (cantParametros != 1)
                {
                    throw new ValidacionException(CamposBackup.Archivo, "La copia de seguridad no es válida: no pertenece a este sistema.");
                }

                administradoresActivos = Convert.ToInt32(Escalar(conn, "SELECT count(*) FROM Usuarios WHERE Activo = 1 AND Rol = 1;"));
                if (administradoresActivos == 0)
                {
                    throw new ReglaNegocioException("BAK_SIN_ADMIN", "La copia no tiene ningún administrador activo: después de restaurarla nadie podría administrar el sistema.");
                }

                ganaderos = Convert.ToInt32(Escalar(conn, "SELECT count(*) FROM Ganaderos;"));
                clientes = Convert.ToInt32(Escalar(conn, "SELECT count(*) FROM Clientes;"));
                usuariosActivos = Convert.ToInt32(Escalar(conn, "SELECT count(*) FROM Usuarios WHERE Activo = 1;"));
            }
            catch (SqliteException ex)
            {
                if (ex.SqliteErrorCode == 26)
                {
                    throw new ValidacionException(CamposBackup.Archivo, "La copia de seguridad no es válida: no contiene una base de datos.");
                }

                throw new ValidacionException(CamposBackup.Archivo, "La copia de seguridad no es válida: la base de datos está dañada.");
            }

            // 5. Migración de prueba
            if (requiereActualizar)
            {
                var prueba = Path.Combine(carpeta, "prueba-migracion.db");
                try
                {
                    File.Copy(Path.Combine(carpeta, "pesaje.db"), prueba);
                    var opciones = new DbContextOptionsBuilder<PesajeDbContext>();
                    opciones.ConfigurarSqlite(new SqliteConnectionStringBuilder { DataSource = prueba, Pooling = false }.ToString());
                    await using (var ctx = new PesajeDbContext(opciones.Options))
                    {
                        await ctx.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
                    }

                    using var connPrueba = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = prueba, Pooling = false }.ToString());
                    connPrueba.Open();
                    if (!string.Equals(Escalar(connPrueba, "PRAGMA quick_check;"), "ok", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("Fallo quick_check en prueba-migracion");
                    }
                }
                catch (Exception ex) when (ex is not ReglaNegocioException && ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Falló la migración de prueba sobre la copia {Archivo}.", Path.GetFileName(rutaBackup));
                    throw new ReglaNegocioException("BAK_NO_MIGRA", "La copia no se puede actualizar al formato de esta versión del sistema.", ex);
                }
                finally
                {
                    SqliteConnection.ClearAllPools();
                    if (File.Exists(prueba))
                    {
                        try { File.Delete(prueba); } catch { }
                    }
                }
            }

            // 6. Advertencias
            var advertencias = new List<string>();
            if (requiereActualizar)
            {
                advertencias.Add("La copia es de una versión anterior del sistema: se actualizará al iniciar.");
            }

            if (!string.Equals(manifiesto.Equipo, _nombreEquipo, StringComparison.OrdinalIgnoreCase))
            {
                advertencias.Add($"La copia se creó en el equipo «{manifiesto.Equipo}». Después de restaurar revise la configuración de la balanza.");
            }

            if (!string.Equals(manifiesto.VersionAplicacion, VersionAplicacion, StringComparison.Ordinal))
            {
                advertencias.Add($"La copia se creó con la versión {manifiesto.VersionAplicacion} del sistema (esta es la {VersionAplicacion}).");
            }

            var ahoraUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var dias = (ahoraUtc - manifiesto.CreadoEnUtc).TotalDays;
            if (dias >= 1.0)
            {
                advertencias.Add($"La copia tiene {((int)dias).ToString(TextosBoleta.Cultura)} día(s) de antigüedad: se perderá lo registrado después.");
            }

            _logger.LogInformation("Copia de seguridad validada para restaurar: {Archivo} (Token: {Token}).", Path.GetFileName(rutaBackup), token);
            exito = true;

            return new ValidacionRestauracion(
                token,
                Info(rutaBackup, manifiesto),
                ganaderos,
                clientes,
                usuariosActivos,
                administradoresActivos,
                requiereActualizar,
                advertencias);
        }
        finally
        {
            if (!exito && Directory.Exists(carpeta))
            {
                try { Directory.Delete(carpeta, true); } catch { }
            }
        }
    }

    public Task DescartarValidacionAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || !RegexValidacionToken.IsMatch(token))
        {
            throw new ValidacionException(CamposBackup.Archivo, "Identificador de validación no válido.");
        }

        var carpeta = Path.Combine(_appPaths.RestoreDir, token);
        if (Directory.Exists(carpeta))
        {
            try { Directory.Delete(carpeta, true); } catch { }
        }

        return Task.CompletedTask;
    }

    public async Task<InfoBackup> ProgramarRestauracionAsync(string token, string solicitadoPor, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solicitadoPor);
        if (string.IsNullOrWhiteSpace(token) || !RegexValidacionToken.IsMatch(token))
        {
            throw new ValidacionException(CamposBackup.Archivo, "Identificador de validación no válido.");
        }

        await _bloqueo.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => ProgramarRestauracionInterno(token, solicitadoPor, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _bloqueo.Release();
        }
    }

    private InfoBackup ProgramarRestauracionInterno(string token, string solicitadoPor, CancellationToken cancellationToken)
    {
        var carpetaToken = Path.Combine(_appPaths.RestoreDir, token);
        var rutaDb = Path.Combine(carpetaToken, "pesaje.db");
        var rutaManifest = Path.Combine(carpetaToken, "manifest.json");
        var rutaOrigen = Path.Combine(carpetaToken, "origen.txt");

        if (!Directory.Exists(carpetaToken) || !File.Exists(rutaDb) || !File.Exists(rutaManifest) || !File.Exists(rutaOrigen))
        {
            throw new ReglaNegocioException("BAK_VALIDACION_VENCIDA", "La validación de la copia ya no está disponible. Vuelva a validarla.");
        }

        var manifestJson = File.ReadAllBytes(rutaManifest);
        var manifiesto = JsonSerializer.Deserialize<ManifiestoBackup>(manifestJson, JsonBackup.Opciones);
        var origenTxt = File.ReadAllText(rutaOrigen, Encoding.UTF8);

        if (manifiesto is null)
        {
            throw new ReglaNegocioException("BAK_VALIDACION_VENCIDA", "La validación de la copia ya no está disponible. Vuelva a validarla.");
        }

        string shaCalculado;
        using (var fs = new FileStream(rutaDb, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            shaCalculado = Convert.ToHexString(SHA256.HashData(fs));
        }

        if (!string.Equals(shaCalculado, manifiesto.Sha256BaseDatos, StringComparison.OrdinalIgnoreCase))
        {
            throw new ReglaNegocioException("BAK_VALIDACION_VENCIDA", "La validación de la copia ya no está disponible. Vuelva a validarla.");
        }

        // 3. Resguardo
        ResultadoCreacionBackup seguridad;
        try
        {
            seguridad = CrearInternoAsync(new SolicitudBackup(TipoBackup.PreRestauracion, 0, null), cancellationToken);
        }
        catch (Exception ex)
        {
            throw new ReglaNegocioException("BAK_SIN_RESGUARDO", "No se pudo crear la copia de resguardo de la base actual; no se programó la restauración.", ex);
        }

        // 4. Borrar marcador y pendiente-* anteriores
        var rutaMarcador = Path.Combine(_appPaths.RestoreDir, "pendiente.json");
        if (File.Exists(rutaMarcador))
        {
            try { File.Delete(rutaMarcador); } catch { }
        }

        if (Directory.Exists(_appPaths.RestoreDir))
        {
            foreach (var d in Directory.GetDirectories(_appPaths.RestoreDir, "pendiente-*"))
            {
                try { Directory.Delete(d, true); } catch { }
            }
        }

        // 5. Mover a pendiente-*
        var pendiente = $"pendiente-{Guid.NewGuid():N}";
        var carpetaPendiente = Path.Combine(_appPaths.RestoreDir, pendiente);
        Directory.Move(carpetaToken, carpetaPendiente);

        // 6. Marcador
        var ahoraUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var marcador = new MarcadorRestauracion(
            Version: 1,
            Carpeta: pendiente,
            Sha256BaseDatos: shaCalculado,
            ArchivoOrigen: origenTxt.Trim(),
            FechaBackupUtc: manifiesto.CreadoEnUtc,
            SolicitadoPor: solicitadoPor,
            SolicitadoEnUtc: ahoraUtc);

        var rutaMarcadorTmp = Path.Combine(_appPaths.RestoreDir, "pendiente.json.tmp");
        File.WriteAllBytes(rutaMarcadorTmp, JsonSerializer.SerializeToUtf8Bytes(marcador, JsonBackup.Opciones));
        File.Move(rutaMarcadorTmp, rutaMarcador, overwrite: true);

        // 7. Log y retorno
        _logger.LogInformation(
            "Restauración programada desde {Archivo} por el usuario {Usuario}.",
            Path.GetFileName(origenTxt.Trim()),
            solicitadoPor);

        return seguridad.Backup;
    }

    private static InfoBackup Info(string ruta, ManifiestoBackup m)
    {
        var fi = new FileInfo(ruta);
        return new InfoBackup(
            ruta,
            Path.GetFileName(ruta),
            m.Tipo,
            m.CreadoEnUtc,
            fi.Length,
            m.VersionAplicacion,
            m.Equipo,
            m.UltimaMigracion,
            m.Pesajes,
            m.UltimoNumeroBoleta,
            Valido: true,
            Problema: null);
    }

    private static void EjecutarNoConsulta(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static string? Escalar(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar()?.ToString();
    }

    private sealed record EstadisticasBaseDatos(
        string UltimaMigracion,
        IReadOnlyList<string> Migraciones,
        int Pesajes,
        int Ganaderos,
        int Clientes,
        int Usuarios,
        long? UltimoNumeroBoleta);

    private static EstadisticasBaseDatos LeerEstadisticas(SqliteConnection conn)
    {
        int pesajes = Convert.ToInt32(Escalar(conn, "SELECT count(*) FROM Pesajes;"));
        int ganaderos = Convert.ToInt32(Escalar(conn, "SELECT count(*) FROM Ganaderos;"));
        int clientes = Convert.ToInt32(Escalar(conn, "SELECT count(*) FROM Clientes;"));
        int usuarios = Convert.ToInt32(Escalar(conn, "SELECT count(*) FROM Usuarios;"));

        long? ultimoNumeroBoleta = null;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT max(NumeroBoleta) FROM Pesajes;";
            var res = cmd.ExecuteScalar();
            if (res is not null && res != DBNull.Value)
            {
                ultimoNumeroBoleta = Convert.ToInt64(res);
            }
        }

        var migraciones = new List<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                migraciones.Add(reader.GetString(0));
            }
        }

        var ultimaMigracion = migraciones.Count > 0 ? migraciones[^1] : string.Empty;

        return new EstadisticasBaseDatos(
            ultimaMigracion,
            migraciones,
            pesajes,
            ganaderos,
            clientes,
            usuarios,
            ultimoNumeroBoleta);
    }
}
