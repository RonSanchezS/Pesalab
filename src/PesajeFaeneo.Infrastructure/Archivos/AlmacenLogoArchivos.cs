using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Application.Parametros;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Infrastructure.Common;

namespace PesajeFaeneo.Infrastructure.Archivos;

public class AlmacenLogoArchivos : IAlmacenLogo
{
    public const int TamanoMaximoBytes = 1_048_576; // 1 MiB
    private static readonly HashSet<string> ExtensionesPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp"
    };

    private static readonly byte[] FirmaPng = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] FirmaJpeg = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] FirmaBmp = { 0x42, 0x4D };

    private readonly AppPaths _appPaths;
    private readonly ILogger<AlmacenLogoArchivos> _logger;

    public AlmacenLogoArchivos(AppPaths appPaths, ILogger<AlmacenLogoArchivos> logger)
    {
        _appPaths = appPaths ?? throw new ArgumentNullException(nameof(appPaths));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> GuardarAsync(string rutaOrigen, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rutaOrigen) || !File.Exists(rutaOrigen))
        {
            throw new ValidacionException(CamposParametros.Logo, "No se encontró el archivo seleccionado.");
        }

        var ext = Path.GetExtension(rutaOrigen);
        if (string.IsNullOrEmpty(ext) || !ExtensionesPermitidas.Contains(ext))
        {
            throw new ValidacionException(CamposParametros.Logo, "El logo debe ser una imagen PNG, JPG o BMP.");
        }

        var fileInfo = new FileInfo(rutaOrigen);
        if (fileInfo.Length <= 0 || fileInfo.Length > TamanoMaximoBytes)
        {
            throw new ValidacionException(CamposParametros.Logo, "El logo debe pesar como máximo 1 MB.");
        }

        byte[] buffer = new byte[8];
        int bytesLeidos;
        await using (var fs = new FileStream(rutaOrigen, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            bytesLeidos = await fs.ReadAsync(buffer.AsMemory(0, 8), cancellationToken);
        }

        if (!ValidarFirma(ext, buffer, bytesLeidos))
        {
            throw new ValidacionException(CamposParametros.Logo, "El archivo no es una imagen válida.");
        }

        Directory.CreateDirectory(_appPaths.LogoDir);

        var extMin = ext.ToLowerInvariant();
        var nombreArchivo = $"logo-{Guid.NewGuid():N}{extMin}";
        var rutaDestino = Path.Combine(_appPaths.LogoDir, nombreArchivo);

        File.Copy(rutaOrigen, rutaDestino, overwrite: false);

        return nombreArchivo;
    }

    public string? ObtenerRutaCompleta(string? nombreArchivo)
    {
        if (string.IsNullOrWhiteSpace(nombreArchivo))
        {
            return null;
        }

        if (nombreArchivo != Path.GetFileName(nombreArchivo) ||
            nombreArchivo.Contains("..") ||
            nombreArchivo.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return null;
        }

        var ruta = Path.Combine(_appPaths.LogoDir, nombreArchivo);
        return File.Exists(ruta) ? ruta : null;
    }

    public void Eliminar(string? nombreArchivo)
    {
        if (string.IsNullOrWhiteSpace(nombreArchivo))
        {
            return;
        }

        if (nombreArchivo != Path.GetFileName(nombreArchivo) ||
            nombreArchivo.Contains("..") ||
            nombreArchivo.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return;
        }

        var ruta = Path.Combine(_appPaths.LogoDir, nombreArchivo);
        try
        {
            if (File.Exists(ruta))
            {
                File.Delete(ruta);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "No se pudo eliminar el archivo de logo {NombreArchivo}", nombreArchivo);
        }
    }

    private static bool ValidarFirma(string extension, byte[] buffer, int bytesLeidos)
    {
        var ext = extension.ToLowerInvariant();
        if (ext == ".png")
        {
            return bytesLeidos >= 8 && buffer.Take(8).SequenceEqual(FirmaPng);
        }
        if (ext == ".jpg" || ext == ".jpeg")
        {
            return bytesLeidos >= 3 && buffer.Take(3).SequenceEqual(FirmaJpeg);
        }
        if (ext == ".bmp")
        {
            return bytesLeidos >= 2 && buffer.Take(2).SequenceEqual(FirmaBmp);
        }

        return false;
    }
}
