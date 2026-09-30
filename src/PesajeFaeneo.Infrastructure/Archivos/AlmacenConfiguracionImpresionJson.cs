using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Application.Boletas;
using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.Infrastructure.Common;

namespace PesajeFaeneo.Infrastructure.Archivos;

public class AlmacenConfiguracionImpresionJson : IAlmacenConfiguracionImpresion
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly AppPaths _appPaths;
    private readonly ILogger<AlmacenConfiguracionImpresionJson> _logger;

    public string NombreEquipo { get; }

    public AlmacenConfiguracionImpresionJson(
        AppPaths appPaths,
        ILogger<AlmacenConfiguracionImpresionJson> logger,
        string nombreEquipo)
    {
        _appPaths = appPaths ?? throw new ArgumentNullException(nameof(appPaths));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(nombreEquipo))
        {
            throw new ArgumentException("El nombre de equipo no puede estar vacío.", nameof(nombreEquipo));
        }

        NombreEquipo = nombreEquipo;
    }

    public async Task<ConfiguracionImpresion?> LeerAsync(CancellationToken cancellationToken = default)
    {
        var ruta = Path.Combine(_appPaths.ConfigDir, "impresion.json");
        if (!File.Exists(ruta))
        {
            return null;
        }

        ArchivoConfiguracion? archivo;
        try
        {
            await using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read);
            archivo = await JsonSerializer.DeserializeAsync<ArchivoConfiguracion>(fs, JsonOptions, cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _logger.LogWarning(ex, "No se pudo leer la configuración de impresión ({Ruta}); se usan los valores por defecto.", ruta);
            return null;
        }

        if (archivo is null)
        {
            _logger.LogWarning("El archivo de configuración de impresión ({Ruta}) está vacío; se usan los valores por defecto.", ruta);
            return null;
        }

        if (archivo.Version != 1)
        {
            _logger.LogWarning("La versión {Version} de la configuración de impresión ({Ruta}) no es compatible; se usan los valores por defecto.", archivo.Version, ruta);
            return null;
        }

        if (!string.Equals(archivo.NombreEquipo, NombreEquipo, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("La configuración de impresión pertenece al equipo {Otro}; se ignora en {Este}.", archivo.NombreEquipo, NombreEquipo);
            return null;
        }

        var config = new ConfiguracionImpresion(
            archivo.NombreImpresora,
            archivo.Formato,
            archivo.NombrePapel,
            archivo.Copias,
            archivo.ImprimirAlGrabar,
            archivo.DesplazamientoXMm,
            archivo.DesplazamientoYMm);

        var (normalizada, errores) = ValidadorConfiguracionImpresion.Validar(config);
        if (errores.Count > 0)
        {
            _logger.LogWarning("La configuración de impresión ({Ruta}) contiene valores no válidos; se usan los valores por defecto.", ruta);
            return null;
        }

        return normalizada;
    }

    public async Task GuardarAsync(ConfiguracionImpresion configuracion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuracion);

        var (normalizada, _) = ValidadorConfiguracionImpresion.Validar(configuracion);

        Directory.CreateDirectory(_appPaths.ConfigDir);
        var destino = Path.Combine(_appPaths.ConfigDir, "impresion.json");
        var tmp = Path.Combine(_appPaths.ConfigDir, "impresion.json.tmp");

        var archivo = new ArchivoConfiguracion(
            1,
            NombreEquipo,
            normalizada.NombreImpresora,
            normalizada.Formato,
            normalizada.NombrePapel,
            normalizada.Copias,
            normalizada.ImprimirAlGrabar,
            normalizada.DesplazamientoXMm,
            normalizada.DesplazamientoYMm);

        try
        {
            await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(fs, archivo, JsonOptions, cancellationToken);
            }

            File.Move(tmp, destino, overwrite: true);
        }
        catch
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
                // Mejor esfuerzo
            }

            throw;
        }
    }

    private sealed record ArchivoConfiguracion(
        int Version,
        string NombreEquipo,
        string? NombreImpresora,
        FormatoBoleta Formato,
        string? NombrePapel,
        int Copias,
        bool ImprimirAlGrabar,
        decimal DesplazamientoXMm,
        decimal DesplazamientoYMm);
}
