namespace PesajeFaeneo.Infrastructure.Utilitarios.Backup;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Utilitarios.Backup;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Infrastructure.Common;

public sealed class AlmacenConfiguracionBackupJson : IAlmacenConfiguracionBackup
{
    private sealed record ArchivoConfiguracionBackup(
        int Version,
        bool AutomaticoActivo,
        bool AlCerrar,
        int DiasRetencion,
        string? CarpetaSecundaria);

    private readonly string _rutaArchivo;
    private readonly ILogger<AlmacenConfiguracionBackupJson> _logger;

    public AlmacenConfiguracionBackupJson(AppPaths appPaths, ILogger<AlmacenConfiguracionBackupJson> logger)
    {
        ArgumentNullException.ThrowIfNull(appPaths);
        _rutaArchivo = Path.Combine(appPaths.ConfigDir, "backup.json");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ConfiguracionBackup> LeerAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_rutaArchivo))
        {
            return ConfiguracionBackup.PorDefecto;
        }

        ArchivoConfiguracionBackup? archivo;
        try
        {
            await using var fs = new FileStream(_rutaArchivo, FileMode.Open, FileAccess.Read, FileShare.Read);
            archivo = await JsonSerializer.DeserializeAsync<ArchivoConfiguracionBackup>(fs, JsonBackup.Opciones, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _logger.LogWarning(ex, "No se pudo leer la configuración de backup en {Ruta}; se usarán los valores por defecto.", _rutaArchivo);
            return ConfiguracionBackup.PorDefecto;
        }

        if (archivo is null || archivo.Version != 1)
        {
            _logger.LogWarning("La configuración de backup en {Ruta} es inválida o incompatible; se usará la predeterminada.", _rutaArchivo);
            return ConfiguracionBackup.PorDefecto;
        }

        var config = new ConfiguracionBackup(
            archivo.AutomaticoActivo,
            archivo.AlCerrar,
            archivo.DiasRetencion,
            archivo.CarpetaSecundaria);

        var errores = ReglasBackup.ValidarConfiguracion(config);
        if (errores.Count > 0)
        {
            _logger.LogWarning("La configuración de backup en {Ruta} tiene valores no válidos; se usará la predeterminada.", _rutaArchivo);
            return ConfiguracionBackup.PorDefecto;
        }

        return config;
    }

    public async Task GuardarAsync(ConfiguracionBackup configuracion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuracion);

        var errores = ReglasBackup.ValidarConfiguracion(configuracion);
        if (errores.Count > 0)
        {
            throw new ValidacionException(errores);
        }

        var archivo = new ArchivoConfiguracionBackup(
            1,
            configuracion.AutomaticoActivo,
            configuracion.AlCerrar,
            configuracion.DiasRetencion,
            configuracion.CarpetaSecundaria);

        var carpeta = Path.GetDirectoryName(_rutaArchivo)!;
        Directory.CreateDirectory(carpeta);

        var rutaTmp = _rutaArchivo + ".tmp";
        try
        {
            await using (var fs = new FileStream(rutaTmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(fs, archivo, JsonBackup.Opciones, cancellationToken).ConfigureAwait(false);
            }

            File.Move(rutaTmp, _rutaArchivo, overwrite: true);
        }
        catch
        {
            try
            {
                if (File.Exists(rutaTmp))
                {
                    File.Delete(rutaTmp);
                }
            }
            catch { }

            throw;
        }
    }
}
