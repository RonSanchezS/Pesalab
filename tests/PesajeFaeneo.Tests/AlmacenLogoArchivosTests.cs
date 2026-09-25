using Microsoft.Extensions.Logging.Abstractions;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Infrastructure.Archivos;
using PesajeFaeneo.Infrastructure.Common;
using Xunit;

namespace PesajeFaeneo.Tests;

public class AlmacenLogoArchivosTests
{
    private static readonly byte[] FirmaPng = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    [Fact]
    public async Task GuardarAsync_PngValido_CopiaYDevuelveNombreConPrefijoLogo()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_logo_" + Guid.NewGuid());
        try
        {
            var appPaths = new AppPaths(tempDir);
            var almacen = new AlmacenLogoArchivos(appPaths, NullLogger<AlmacenLogoArchivos>.Instance);

            var archivoOrigen = Path.Combine(tempDir, "origen.png");
            Directory.CreateDirectory(tempDir);
            var bytes = FirmaPng.Concat(new byte[] { 0x01, 0x02, 0x03, 0x04 }).ToArray();
            await File.WriteAllBytesAsync(archivoOrigen, bytes);

            var nombreResultado = await almacen.GuardarAsync(archivoOrigen);

            Assert.StartsWith("logo-", nombreResultado);
            Assert.EndsWith(".png", nombreResultado);
            var guidTexto = nombreResultado["logo-".Length..^".png".Length];
            Assert.True(Guid.TryParseExact(guidTexto, "N", out _), $"'{guidTexto}' no es un GUID en formato 'N'.");

            var rutaCompleta = almacen.ObtenerRutaCompleta(nombreResultado);
            Assert.NotNull(rutaCompleta);
            Assert.True(File.Exists(rutaCompleta));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GuardarAsync_ExtensionGif_LanzaValidacionException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_logo_" + Guid.NewGuid());
        try
        {
            var appPaths = new AppPaths(tempDir);
            var almacen = new AlmacenLogoArchivos(appPaths, NullLogger<AlmacenLogoArchivos>.Instance);

            var archivoOrigen = Path.Combine(tempDir, "logo.gif");
            Directory.CreateDirectory(tempDir);
            await File.WriteAllBytesAsync(archivoOrigen, new byte[] { 0x47, 0x49, 0x46 });

            var ex = await Assert.ThrowsAsync<ValidacionException>(() => almacen.GuardarAsync(archivoOrigen));
            Assert.Contains("El logo debe ser una imagen PNG, JPG o BMP.", ex.Message);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task GuardarAsync_ArchivoVacio_LanzaValidacionException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_logo_" + Guid.NewGuid());
        try
        {
            var appPaths = new AppPaths(tempDir);
            var almacen = new AlmacenLogoArchivos(appPaths, NullLogger<AlmacenLogoArchivos>.Instance);

            var archivoOrigen = Path.Combine(tempDir, "vacio.png");
            Directory.CreateDirectory(tempDir);
            await File.WriteAllBytesAsync(archivoOrigen, Array.Empty<byte>());

            var ex = await Assert.ThrowsAsync<ValidacionException>(() => almacen.GuardarAsync(archivoOrigen));
            Assert.Contains("El logo debe pesar como máximo 1 MB.", ex.Message);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task GuardarAsync_ArchivoMayorA1MiB_LanzaValidacionException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_logo_" + Guid.NewGuid());
        try
        {
            var appPaths = new AppPaths(tempDir);
            var almacen = new AlmacenLogoArchivos(appPaths, NullLogger<AlmacenLogoArchivos>.Instance);

            var archivoOrigen = Path.Combine(tempDir, "pesado.png");
            Directory.CreateDirectory(tempDir);
            var bytes = new byte[AlmacenLogoArchivos.TamanoMaximoBytes + 10];
            Array.Copy(FirmaPng, bytes, FirmaPng.Length);
            await File.WriteAllBytesAsync(archivoOrigen, bytes);

            var ex = await Assert.ThrowsAsync<ValidacionException>(() => almacen.GuardarAsync(archivoOrigen));
            Assert.Contains("El logo debe pesar como máximo 1 MB.", ex.Message);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task GuardarAsync_PngConTexto_LanzaValidacionExceptionPorFirmaInvalida()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_logo_" + Guid.NewGuid());
        try
        {
            var appPaths = new AppPaths(tempDir);
            var almacen = new AlmacenLogoArchivos(appPaths, NullLogger<AlmacenLogoArchivos>.Instance);

            var archivoOrigen = Path.Combine(tempDir, "falso.png");
            Directory.CreateDirectory(tempDir);
            await File.WriteAllTextAsync(archivoOrigen, "ESTE NO ES UN PNG VALIDO");

            var ex = await Assert.ThrowsAsync<ValidacionException>(() => almacen.GuardarAsync(archivoOrigen));
            Assert.Contains("El archivo no es una imagen válida.", ex.Message);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task GuardarAsync_ArchivoInexistente_LanzaValidacionException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_logo_" + Guid.NewGuid());
        try
        {
            var appPaths = new AppPaths(tempDir);
            var almacen = new AlmacenLogoArchivos(appPaths, NullLogger<AlmacenLogoArchivos>.Instance);

            var archivoOrigen = Path.Combine(tempDir, "no_existe.png");

            var ex = await Assert.ThrowsAsync<ValidacionException>(() => almacen.GuardarAsync(archivoOrigen));
            Assert.Contains("No se encontró el archivo seleccionado.", ex.Message);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ObtenerRutaCompleta_RutasConPathTraversalOAbsolutas_DevuelveNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_logo_" + Guid.NewGuid());
        try
        {
            var appPaths = new AppPaths(tempDir);
            var almacen = new AlmacenLogoArchivos(appPaths, NullLogger<AlmacenLogoArchivos>.Instance);

            Assert.Null(almacen.ObtenerRutaCompleta(@"..\x.png"));
            Assert.Null(almacen.ObtenerRutaCompleta(@"C:\x.png"));
            Assert.Null(almacen.ObtenerRutaCompleta(""));
            Assert.Null(almacen.ObtenerRutaCompleta(null));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Eliminar_ArchivoInexistente_NoLanzaExcepcion()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_logo_" + Guid.NewGuid());
        try
        {
            var appPaths = new AppPaths(tempDir);
            var almacen = new AlmacenLogoArchivos(appPaths, NullLogger<AlmacenLogoArchivos>.Instance);

            // No debe lanzar
            almacen.Eliminar("logo-inexistente.png");
            almacen.Eliminar(null);
            almacen.Eliminar(@"..\invalido.png");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }
}
