using Microsoft.Extensions.Logging;
using PesajeFaeneo.Infrastructure.Archivos;
using PesajeFaeneo.Infrastructure.Reportes;
using PesajeFaeneo.Tests.Utilidades.Reportes;
using Xunit;

namespace PesajeFaeneo.Tests;

public class CargadorLogoReporteTests : IDisposable
{
    private readonly string _directorioTemp;
    private readonly LoggerEspia<CargadorLogoReporteTests> _logger = new();

    public CargadorLogoReporteTests()
    {
        _directorioTemp = Path.Combine(Path.GetTempPath(), "pesalab-f7-logo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directorioTemp);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directorioTemp))
            {
                Directory.Delete(_directorioTemp, recursive: true);
            }
        }
        catch
        {
            // Ignorar errores de limpieza de directorio temporal
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Cargar_RutaNulaOVacia_DevuelveNullYSinAdvertencias(string? ruta)
    {
        var logo = CargadorLogoReporte.Cargar(ruta, _logger);

        Assert.Null(logo);
        Assert.Empty(_logger.Mensajes);
    }

    [Fact]
    public void Cargar_PngValido_DevuelveLogoConDatosEImagen()
    {
        string ruta = DatosReporteDePrueba.CrearLogo(_directorioTemp, ".png", 200, 60);

        var logo = CargadorLogoReporte.Cargar(ruta, _logger);

        Assert.NotNull(logo);
        Assert.Equal(File.ReadAllBytes(ruta), logo.Datos);
        Assert.NotNull(logo.Imagen);
        Assert.Empty(_logger.Mensajes);
    }

    [Fact]
    public void Cargar_JpegValido_DevuelveLogoConDatosEImagen()
    {
        string ruta = DatosReporteDePrueba.CrearLogo(_directorioTemp, ".jpg", 200, 60);

        var logo = CargadorLogoReporte.Cargar(ruta, _logger);

        Assert.NotNull(logo);
        Assert.Equal(File.ReadAllBytes(ruta), logo.Datos);
        Assert.NotNull(logo.Imagen);
        Assert.Empty(_logger.Mensajes);
    }

    [Fact]
    public void Cargar_BmpValido_DevuelveLogoConDatosEImagen()
    {
        string ruta = DatosReporteDePrueba.CrearLogo(_directorioTemp, ".bmp", 200, 60);

        var logo = CargadorLogoReporte.Cargar(ruta, _logger);

        Assert.NotNull(logo);
        Assert.Equal(File.ReadAllBytes(ruta), logo.Datos);
        Assert.NotNull(logo.Imagen);
        Assert.Empty(_logger.Mensajes);
    }

    [Fact]
    public void Cargar_ArchivoNoExiste_DevuelveNullYRegistraAdvertenciaConNombreSolo()
    {
        string ruta = Path.Combine(_directorioTemp, "logo_fantasma.png");

        var logo = CargadorLogoReporte.Cargar(ruta, _logger);

        Assert.Null(logo);
        var msg = Assert.Single(_logger.Mensajes);
        Assert.Equal(LogLevel.Warning, msg.Nivel);
        Assert.Contains("logo_fantasma.png", msg.Mensaje);
        Assert.DoesNotContain(_directorioTemp, msg.Mensaje);
    }

    [Fact]
    public void Cargar_ArchivoSuperaTamanoMaximo_DevuelveNullYRegistraAdvertencia()
    {
        string ruta = Path.Combine(_directorioTemp, "logo_pesado.png");
        byte[] bytes = new byte[AlmacenLogoArchivos.TamanoMaximoBytes + 1];
        File.WriteAllBytes(ruta, bytes);

        var logo = CargadorLogoReporte.Cargar(ruta, _logger);

        Assert.Null(logo);
        var msg = Assert.Single(_logger.Mensajes);
        Assert.Equal(LogLevel.Warning, msg.Nivel);
        Assert.Contains("logo_pesado.png", msg.Mensaje);
        Assert.Contains("supera 1 MB", msg.Mensaje);
        Assert.DoesNotContain(_directorioTemp, msg.Mensaje);
    }

    [Fact]
    public void Cargar_ArchivoBytesAleatorios_DevuelveNullYRegistraAdvertencia()
    {
        string ruta = Path.Combine(_directorioTemp, "logo_invalido.png");
        byte[] bytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        File.WriteAllBytes(ruta, bytes);

        var logo = CargadorLogoReporte.Cargar(ruta, _logger);

        Assert.Null(logo);
        var msg = Assert.Single(_logger.Mensajes);
        Assert.Equal(LogLevel.Warning, msg.Nivel);
        Assert.Contains("logo_invalido.png", msg.Mensaje);
        Assert.Contains("no es una imagen válida", msg.Mensaje);
    }

    [Fact]
    public void Cargar_PngTruncado_DevuelveNullYRegistraAdvertencia()
    {
        string rutaPngReal = DatosReporteDePrueba.CrearLogo(_directorioTemp, ".png", 200, 60);
        byte[] bytesCompletos = File.ReadAllBytes(rutaPngReal);

        // Primeros 40 bytes: cabecera PNG válida pero sin bloques IEND / datos incompletos
        byte[] bytesTruncados = bytesCompletos.Take(40).ToArray();
        string rutaTruncada = Path.Combine(_directorioTemp, "logo_truncado.png");
        File.WriteAllBytes(rutaTruncada, bytesTruncados);

        var logo = CargadorLogoReporte.Cargar(rutaTruncada, _logger);

        Assert.Null(logo);
        var msg = Assert.Single(_logger.Mensajes);
        Assert.Equal(LogLevel.Warning, msg.Nivel);
        Assert.Contains("logo_truncado.png", msg.Mensaje);
        Assert.Contains("no es una imagen válida", msg.Mensaje);
    }

    // R62: QuestPDF decodifica de forma diferida, así que un archivo cortado DESPUÉS de la cabecera pasaba la validación
    // y terminaba como una imagen rota dentro del Excel. El cargador valida la integridad estructural completa.
    [Theory]
    [InlineData(".png", 2)]    // cortado a la mitad
    [InlineData(".png", -20)]  // sin los últimos 20 bytes (cae dentro del IEND / último IDAT)
    [InlineData(".png", -1)]   // falta un solo byte
    [InlineData(".jpg", 2)]
    [InlineData(".jpg", -20)]
    [InlineData(".bmp", 2)]
    [InlineData(".bmp", -20)]
    public void Cargar_ArchivoCortadoDespuesDeLaCabecera_DevuelveNullYRegistraAdvertencia(string extension, int corte)
    {
        string rutaReal = DatosReporteDePrueba.CrearLogo(_directorioTemp, extension, 200, 60);
        byte[] completos = File.ReadAllBytes(rutaReal);
        int largo = corte > 0 ? completos.Length / corte : completos.Length + corte;
        string rutaCortada = Path.Combine(_directorioTemp, "logo_cortado" + extension);
        File.WriteAllBytes(rutaCortada, completos.Take(largo).ToArray());

        var logo = CargadorLogoReporte.Cargar(rutaCortada, _logger);

        Assert.Null(logo);
        var msg = Assert.Single(_logger.Mensajes);
        Assert.Equal(LogLevel.Warning, msg.Nivel);
        Assert.Contains("logo_cortado" + extension, msg.Mensaje);
        Assert.Contains("no es una imagen válida", msg.Mensaje);
    }

    [Fact]
    public void Cargar_ArchivoQueDesapareceEntreLaComprobacionYLaLectura_NoLanza()
    {
        // Una ruta que no existe nunca debe lanzar: devuelve null con advertencia.
        string ruta = Path.Combine(_directorioTemp, "no_esta_aqui.png");

        var logo = CargadorLogoReporte.Cargar(ruta, _logger);

        Assert.Null(logo);
        Assert.Single(_logger.Mensajes);
    }

    [Fact]
    public void Cargar_ArchivoBloqueadoSinPermisoLectura_DevuelveNullYRegistraAdvertencia()
    {
        string ruta = DatosReporteDePrueba.CrearLogo(_directorioTemp, ".png", 200, 60);

        using (var fs = new FileStream(ruta, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var logo = CargadorLogoReporte.Cargar(ruta, _logger);

            Assert.Null(logo);
            var msg = Assert.Single(_logger.Mensajes);
            Assert.Equal(LogLevel.Warning, msg.Nivel);
            Assert.Contains(Path.GetFileName(ruta), msg.Mensaje);
            Assert.Contains("no se pudo leer", msg.Mensaje);
            Assert.DoesNotContain(_directorioTemp, msg.Mensaje);
        }
    }
}
