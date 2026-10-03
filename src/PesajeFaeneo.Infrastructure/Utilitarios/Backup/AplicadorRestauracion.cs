namespace PesajeFaeneo.Infrastructure.Utilitarios.Backup;

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Utilitarios.Backup;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Common;
using PesajeFaeneo.Infrastructure.Persistencia;

public sealed class AplicadorRestauracion : IAplicadorRestauracion
{
    private static readonly Regex RegexCarpetaPendiente = new(
        @"^pendiente-[0-9a-f]{32}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly AppPaths _appPaths;
    private readonly IDbContextFactory<PesajeDbContext> _contextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AplicadorRestauracion> _logger;

    public AplicadorRestauracion(
        AppPaths appPaths,
        IDbContextFactory<PesajeDbContext> contextFactory,
        TimeProvider timeProvider,
        ILogger<AplicadorRestauracion> logger)
    {
        _appPaths = appPaths;
        _contextFactory = contextFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public ResultadoRestauracionArranque AplicarPendiente()
    {
        try
        {
            return AplicarPendienteInterno();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al aplicar la restauración pendiente.");
            var motivo = ex is IOException or UnauthorizedAccessException
                ? "otro programa está usando la base de datos"
                : "error inesperado";
            return Fallida($"No se pudo aplicar la restauración ({motivo}). Se sigue usando la base de datos actual. Vuelva a intentarlo desde Utilitarios › Backup / Restaurar.");
        }
    }

    private ResultadoRestauracionArranque AplicarPendienteInterno()
    {
        // 0. Limpieza (mejor esfuerzo): borrar todas las carpetas validacion-*
        try
        {
            if (Directory.Exists(_appPaths.RestoreDir))
            {
                foreach (var dir in Directory.GetDirectories(_appPaths.RestoreDir, "validacion-*"))
                {
                    try { Directory.Delete(dir, true); } catch { }
                }
            }
        }
        catch { }

        var rutaMarcador = Path.Combine(_appPaths.RestoreDir, "pendiente.json");

        // 1. Sin marcador
        if (!File.Exists(rutaMarcador))
        {
            try
            {
                if (Directory.Exists(_appPaths.RestoreDir))
                {
                    foreach (var dir in Directory.GetDirectories(_appPaths.RestoreDir, "pendiente-*"))
                    {
                        try { Directory.Delete(dir, true); } catch { }
                    }
                }
            }
            catch { }

            return ResultadoRestauracionArranque.NoHabia;
        }

        // 2. Leer marcador
        MarcadorRestauracion? marcador = null;
        try
        {
            var json = File.ReadAllText(rutaMarcador);
            marcador = JsonSerializer.Deserialize<MarcadorRestauracion>(json, JsonBackup.Opciones);
        }
        catch
        {
            // Ignorado, se valida abajo
        }

        if (marcador is null || marcador.Version != 1 || string.IsNullOrWhiteSpace(marcador.Carpeta) || !RegexCarpetaPendiente.IsMatch(marcador.Carpeta))
        {
            try { File.Delete(rutaMarcador); } catch { }
            return Fallida("El archivo de restauración pendiente está dañado; no se restauró nada.");
        }

        // Borrar pendiente-* huérfanas distintas a la del marcador
        try
        {
            foreach (var dir in Directory.GetDirectories(_appPaths.RestoreDir, "pendiente-*"))
            {
                if (!string.Equals(Path.GetFileName(dir), marcador.Carpeta, StringComparison.OrdinalIgnoreCase))
                {
                    try { Directory.Delete(dir, true); } catch { }
                }
            }
        }
        catch { }

        // 3. Validar carpeta pendiente y su base
        var carpeta = Path.Combine(_appPaths.RestoreDir, marcador.Carpeta);
        var dbPendiente = Path.Combine(carpeta, "pesaje.db");

        bool valida = false;
        if (Directory.Exists(carpeta) && File.Exists(dbPendiente))
        {
            try
            {
                var sha = CalcularSha256(dbPendiente);
                if (string.Equals(sha, marcador.Sha256BaseDatos, StringComparison.OrdinalIgnoreCase))
                {
                    var sb = new SqliteConnectionStringBuilder
                    {
                        DataSource = dbPendiente,
                        Mode = SqliteOpenMode.ReadOnly,
                        Pooling = false
                    };
                    using var conn = new SqliteConnection(sb.ToString());
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "PRAGMA quick_check;";
                    var res = cmd.ExecuteScalar()?.ToString();
                    if (string.Equals(res, "ok", StringComparison.OrdinalIgnoreCase))
                    {
                        valida = true;
                    }
                }
            }
            catch
            {
                valida = false;
            }
        }

        if (!valida)
        {
            try { File.Delete(rutaMarcador); } catch { }
            if (Directory.Exists(carpeta))
            {
                try { Directory.Delete(carpeta, true); } catch { }
            }
            return Fallida("La copia preparada para restaurar no está completa o fue alterada; no se restauró nada.");
        }

        // 4. Limpiar pools SQLite
        SqliteConnection.ClearAllPools();

        // 5–7. Preparar reemplazo y mover archivos con soporte de Deshacer
        var local = TimeZoneInfo.ConvertTimeFromUtc(_timeProvider.GetUtcNow().UtcDateTime, _timeProvider.LocalTimeZone);
        var prefijoReemplazada = $"reemplazada-{local.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}";
        var carpetaReemplazada = Path.Combine(_appPaths.RestoreDir, prefijoReemplazada);
        int sufijo = 2;
        while (Directory.Exists(carpetaReemplazada))
        {
            carpetaReemplazada = Path.Combine(_appPaths.RestoreDir, $"{prefijoReemplazada}_{sufijo++}");
        }

        Directory.CreateDirectory(carpetaReemplazada);

        var movimientos = new List<(string Origen, string Destino)>();
        bool logoMovido = false;
        bool baseMovida = false;

        try
        {
            // Mover pesaje.db, pesaje.db-wal, pesaje.db-shm
            var extensiones = new[] { "", "-wal", "-shm" };
            foreach (var ext in extensiones)
            {
                var rutaOriginal = _appPaths.DatabasePath + ext;
                if (File.Exists(rutaOriginal))
                {
                    var nombre = Path.GetFileName(rutaOriginal);
                    var destino = Path.Combine(carpetaReemplazada, nombre);
                    MoverConReintentos(rutaOriginal, destino);
                    movimientos.Add((rutaOriginal, destino));
                    if (ext == "")
                    {
                        baseMovida = true;
                    }
                }
            }

            // Mover logo
            var destinoLogoReemplazada = Path.Combine(carpetaReemplazada, "logo");
            if (Directory.Exists(_appPaths.LogoDir))
            {
                Directory.Move(_appPaths.LogoDir, destinoLogoReemplazada);
                logoMovido = true;
            }
            Directory.CreateDirectory(_appPaths.LogoDir);

            // 6. Copiar base restaurada y logos nuevos
            File.Copy(dbPendiente, _appPaths.DatabasePath);

            var carpetaLogoPendiente = Path.Combine(carpeta, "logo");
            if (Directory.Exists(carpetaLogoPendiente))
            {
                foreach (var archivo in Directory.GetFiles(carpetaLogoPendiente))
                {
                    var dest = Path.Combine(_appPaths.LogoDir, Path.GetFileName(archivo));
                    File.Copy(archivo, dest, overwrite: true);
                }
            }

            // 7. Validar SHA de la base copiada
            var shaCopiada = CalcularSha256(_appPaths.DatabasePath);
            if (!string.Equals(shaCopiada, marcador.Sha256BaseDatos, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("El SHA-256 de la base de datos copiada no coincide con el resguardo.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo durante la copia o verificación de la base restaurada; iniciando reversión...");

            // 9. Deshacer
            if (baseMovida && File.Exists(_appPaths.DatabasePath))
            {
                try { File.Delete(_appPaths.DatabasePath); } catch { }
                var wal = _appPaths.DatabasePath + "-wal";
                if (File.Exists(wal)) try { File.Delete(wal); } catch { }
                var shm = _appPaths.DatabasePath + "-shm";
                if (File.Exists(shm)) try { File.Delete(shm); } catch { }
            }

            // Devolver archivos originales en orden inverso
            for (int i = movimientos.Count - 1; i >= 0; i--)
            {
                var mov = movimientos[i];
                if (File.Exists(mov.Destino))
                {
                    try { File.Move(mov.Destino, mov.Origen, overwrite: true); } catch { }
                }
            }

            if (logoMovido)
            {
                if (Directory.Exists(_appPaths.LogoDir))
                {
                    try { Directory.Delete(_appPaths.LogoDir, true); } catch { }
                }
                var destinoLogoReemplazada = Path.Combine(carpetaReemplazada, "logo");
                if (Directory.Exists(destinoLogoReemplazada))
                {
                    try { Directory.Move(destinoLogoReemplazada, _appPaths.LogoDir); } catch { }
                }
            }

            try { File.Delete(rutaMarcador); } catch { }
            if (Directory.Exists(carpeta))
            {
                try { Directory.Delete(carpeta, true); } catch { }
            }

            var motivoCorto = ex is IOException or UnauthorizedAccessException
                ? "otro programa está usando la base de datos"
                : "error inesperado";
            return Fallida($"No se pudo aplicar la restauración ({motivoCorto}). Se sigue usando la base de datos actual. Vuelva a intentarlo desde Utilitarios › Backup / Restaurar.");
        }

        // 8. Éxito: borrar marcador y carpeta pendiente
        try { File.Delete(rutaMarcador); } catch { }
        if (Directory.Exists(carpeta))
        {
            try { Directory.Delete(carpeta, true); } catch { }
        }

        // Retención de carpetas reemplazada-*
        try
        {
            var carpetasReemplazadas = Directory.GetDirectories(_appPaths.RestoreDir, "reemplazada-*")
                .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)
                .ToList();

            var aEliminar = carpetasReemplazadas.Count - ReglasBackup.ReemplazadasConservadas;
            for (int i = 0; i < aEliminar; i++)
            {
                try { Directory.Delete(carpetasReemplazadas[i], true); } catch { }
            }
        }
        catch { }

        _logger.LogInformation(
            "Restauración aplicada desde {Archivo} (solicitada por {Usuario}).",
            marcador.ArchivoOrigen,
            marcador.SolicitadoPor);

        return new ResultadoRestauracionArranque(
            EstadoRestauracionArranque.Aplicada,
            null,
            marcador.ArchivoOrigen,
            marcador.FechaBackupUtc,
            marcador.SolicitadoPor,
            carpetaReemplazada);
    }

    public void Revertir(ResultadoRestauracionArranque aplicada)
    {
        if (aplicada.Estado != EstadoRestauracionArranque.Aplicada ||
            string.IsNullOrWhiteSpace(aplicada.CarpetaReemplazada) ||
            !Directory.Exists(aplicada.CarpetaReemplazada))
        {
            throw new InvalidOperationException("No se puede revertir: la restauración no fue aplicada o la carpeta de respaldo no existe.");
        }

        SqliteConnection.ClearAllPools();

        var local = TimeZoneInfo.ConvertTimeFromUtc(_timeProvider.GetUtcNow().UtcDateTime, _timeProvider.LocalTimeZone);
        var prefijoFallida = $"fallida-{local.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}";
        var carpetaFallida = Path.Combine(_appPaths.RestoreDir, prefijoFallida);
        int sufijo = 2;
        while (Directory.Exists(carpetaFallida))
        {
            carpetaFallida = Path.Combine(_appPaths.RestoreDir, $"{prefijoFallida}_{sufijo++}");
        }

        Directory.CreateDirectory(carpetaFallida);

        // Mover pesaje.db, pesaje.db-wal, pesaje.db-shm actuales a fallida
        var extensiones = new[] { "", "-wal", "-shm" };
        foreach (var ext in extensiones)
        {
            var ruta = _appPaths.DatabasePath + ext;
            if (File.Exists(ruta))
            {
                File.Move(ruta, Path.Combine(carpetaFallida, Path.GetFileName(ruta)));
            }
        }

        // Mover los de reemplazada de vuelta a BaseDir
        foreach (var ext in extensiones)
        {
            var nombre = Path.GetFileName(_appPaths.DatabasePath + ext);
            var rutaEnReemplazada = Path.Combine(aplicada.CarpetaReemplazada, nombre);
            if (File.Exists(rutaEnReemplazada))
            {
                File.Move(rutaEnReemplazada, _appPaths.DatabasePath + ext);
            }
        }

        // Logo: actual -> fallida/logo
        if (Directory.Exists(_appPaths.LogoDir))
        {
            Directory.Move(_appPaths.LogoDir, Path.Combine(carpetaFallida, "logo"));
        }

        // Logo: reemplazada/logo -> LogoDir
        var logoEnReemplazada = Path.Combine(aplicada.CarpetaReemplazada, "logo");
        if (Directory.Exists(logoEnReemplazada))
        {
            Directory.Move(logoEnReemplazada, _appPaths.LogoDir);
        }
        else
        {
            Directory.CreateDirectory(_appPaths.LogoDir);
        }

        _logger.LogError("Se revirtió la restauración: la base restaurada no pudo inicializarse.");
    }

    public async Task RegistrarAuditoriaAsync(ResultadoRestauracionArranque aplicada, CancellationToken cancellationToken = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        ctx.Auditorias.Add(FabricaAuditoria.Crear(
            _timeProvider,
            usuarioId: null,
            AccionAuditoria.Restauracion,
            EntidadesAuditoria.Sistema,
            "Restauracion",
            despues: new
            {
                aplicada.ArchivoOrigen,
                aplicada.FechaBackupUtc,
                aplicada.SolicitadoPor
            },
            motivo: "Restauración aplicada al iniciar el sistema"));

        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Tras un reinicio, el proceso anterior puede seguir cerrándose y conservar un instante la base abierta:
    /// se reintenta unos segundos antes de rendirse (y descartar una restauración ya validada).
    /// </summary>
    private static void MoverConReintentos(string origen, string destino)
    {
        const int intentos = 8;
        for (var intento = 1; ; intento++)
        {
            try
            {
                File.Move(origen, destino);
                return;
            }
            catch (IOException) when (intento < intentos)
            {
                SqliteConnection.ClearAllPools();
                Thread.Sleep(400);
            }
        }
    }

    private static string CalcularSha256(string ruta)
    {
        using var stream = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static ResultadoRestauracionArranque Fallida(string mensaje)
    {
        return new ResultadoRestauracionArranque(
            EstadoRestauracionArranque.Fallida,
            mensaje,
            null,
            null,
            null,
            null);
    }
}
