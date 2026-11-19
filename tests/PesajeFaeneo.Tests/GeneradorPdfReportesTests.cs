using System.Drawing;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PesajeFaeneo.Application.Reportes.Dtos;
using PesajeFaeneo.Infrastructure.Reportes;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Reportes;
using Xunit;

namespace PesajeFaeneo.Tests;

public class GeneradorPdfReportesTests : IDisposable
{
    private readonly string _directorioTemp;

    public GeneradorPdfReportesTests()
    {
        _directorioTemp = Path.Combine(Path.GetTempPath(), "pesalab-f7-pdf-" + Guid.NewGuid().ToString("N"));
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
            // Ignorar errores de limpieza
        }
    }

    [Fact]
    public async Task GenerarPdfAsync_EscribePdfValido_PrimerosBytesSonPdfYStreamPermaneceAbierto()
    {
        var generador = new GeneradorPdfReportes(NullLogger<GeneradorPdfReportes>.Instance);
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 5);
        using var stream = new MemoryStream();

        await generador.GenerarPdfAsync(reporte, stream);

        Assert.True(stream.CanRead);
        Assert.True(stream.CanSeek);
        Assert.True(stream.Length > 0);

        stream.Position = 0;
        byte[] encabezado = new byte[5];
        int leidos = stream.Read(encabezado, 0, 5);
        Assert.Equal(5, leidos);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(encabezado));
    }

    [Fact]
    public async Task GenerarPaginasAsync_Reporte5Filas_DevuelveUnaPaginaConFirmaPng()
    {
        var generador = new GeneradorPdfReportes(NullLogger<GeneradorPdfReportes>.Instance);
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 5);

        var paginas = await generador.GenerarPaginasAsync(reporte, 72);

        var pagina = Assert.Single(paginas);
        Assert.True(pagina.Length > 8);
        byte[] firmaPngEsperada = [0x89, 0x50, 0x4E, 0x47];
        Assert.Equal(firmaPngEsperada, pagina.Take(4).ToArray());
    }

    [Fact]
    public async Task GenerarPaginasAsync_1500Filas_GeneraMasDeUnaPaginaYCuentaEsEstable()
    {
        var generador = new GeneradorPdfReportes(NullLogger<GeneradorPdfReportes>.Instance);
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 1500);

        var paginas1 = await generador.GenerarPaginasAsync(reporte, 72);
        var paginas2 = await generador.GenerarPaginasAsync(reporte, 72);

        Assert.True(paginas1.Count > 1);
        Assert.Equal(paginas1.Count, paginas2.Count);
    }

    [Fact]
    public async Task GenerarPdfAsync_DatosExtremos_GeneraSinExcepcion()
    {
        var generador = new GeneradorPdfReportes(NullLogger<GeneradorPdfReportes>.Instance);
        string direccionLarga = new string('A', 150);
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 5, direccion: direccionLarga);
        using var stream = new MemoryStream();

        await generador.GenerarPdfAsync(reporte, stream);

        Assert.True(stream.Length > 0);
    }

    [Fact]
    public async Task GenerarPdfAsync_AnuladasConMotivoYUsuarioLargos_GeneraUnaPaginaSinExcepcion()
    {
        var generador = new GeneradorPdfReportes(NullLogger<GeneradorPdfReportes>.Instance);
        string motivoLargo = new string('M', 100);
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.Anuladas, 5, motivo: motivoLargo);
        using var stream = new MemoryStream();

        await generador.GenerarPdfAsync(reporte, stream);
        var paginas = await generador.GenerarPaginasAsync(reporte, 72);

        Assert.True(stream.Length > 0);
        Assert.Single(paginas);
    }

    [Fact]
    public async Task GenerarPdfAsync_ReporteVacio_GeneraPdfValidoDeUnaPagina()
    {
        var generador = new GeneradorPdfReportes(NullLogger<GeneradorPdfReportes>.Instance);
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 0);
        using var stream = new MemoryStream();

        await generador.GenerarPdfAsync(reporte, stream);
        var paginas = await generador.GenerarPaginasAsync(reporte, 72);

        Assert.True(stream.Length > 0);
        Assert.Single(paginas);
    }

    [Fact]
    public async Task GenerarPdfAsync_CaracteresEspeciales_ConValidacionDeGlifos_GeneraSinExcepcion()
    {
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = true;
        try
        {
            var generador = new GeneradorPdfReportes(NullLogger<GeneradorPdfReportes>.Instance);
            var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 5, direccion: "AV. JOSÉ PEÑA N° 123 — ÁÉÍÓÚ ñ …");
            using var stream = new MemoryStream();

            await generador.GenerarPdfAsync(reporte, stream);

            Assert.True(stream.Length > 0);
        }
        finally
        {
            QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
        }
    }

    [Fact]
    public async Task GenerarPdfAsync_ConBoletasSimuladas_GeneraSinExcepcion()
    {
        var generador = new GeneradorPdfReportes(NullLogger<GeneradorPdfReportes>.Instance);
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 5, simuladas: 2);
        using var stream = new MemoryStream();

        await generador.GenerarPdfAsync(reporte, stream);

        Assert.True(stream.Length > 0);
    }

    [Fact]
    public async Task GeneradorPdfReportes_ValidacionArgumentos()
    {
        var generador = new GeneradorPdfReportes(NullLogger<GeneradorPdfReportes>.Instance);
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 2);

        await Assert.ThrowsAsync<ArgumentNullException>(() => generador.GenerarPdfAsync(null!, new MemoryStream()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => generador.GenerarPdfAsync(reporte, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => generador.GenerarPaginasAsync(null!, 72));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => generador.GenerarPaginasAsync(reporte, 50));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => generador.GenerarPaginasAsync(reporte, 700));
        Assert.Throws<ArgumentNullException>(() => new GeneradorPdfReportes(null!));
    }

    [Fact]
    public void GenerarPdfAsync_NoCorreEnHiloQueLlama_EjecutaEnThreadPool()
    {
        HiloSta.Ejecutar(() =>
        {
            int staThreadId = Environment.CurrentManagedThreadId;
            var generador = new GeneradorPdfReportes(NullLogger<GeneradorPdfReportes>.Instance);
            var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 5);
            using var stream = new StreamQueAnotaHilo();

            generador.GenerarPdfAsync(reporte, stream).GetAwaiter().GetResult();

            Assert.NotEmpty(stream.Invocaciones);
            Assert.All(stream.Invocaciones, inv =>
            {
                Assert.True(inv.EsPool, "La escritura debió realizarse en un hilo del ThreadPool.");
                Assert.NotEqual(staThreadId, inv.ThreadId);
            });
        });
    }

    [Theory]
    [InlineData(".png", 400, 120)]
    [InlineData(".png", 120, 400)]
    [InlineData(".jpg", 400, 120)]
    [InlineData(".bmp", 400, 120)]
    public async Task GenerarPdfAsync_ConFormatosDeLogo_GeneraSinExcepcion(string extension, int ancho, int alto)
    {
        string rutaLogo = DatosReporteDePrueba.CrearLogo(_directorioTemp, extension, ancho, alto);
        var logger = new LoggerEspia<GeneradorPdfReportes>();
        var generador = new GeneradorPdfReportes(logger);
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 5, rutaLogo: rutaLogo);
        using var stream = new MemoryStream();

        await generador.GenerarPdfAsync(reporte, stream);

        Assert.True(stream.Length > 0);
        // Un logo válido no debe descartarse en silencio: ninguna advertencia.
        Assert.Empty(logger.Mensajes);
    }

    [Fact]
    public async Task GenerarPdfAsync_1500FilasConLogo_GeneraMasDeUnaPaginaSinExcepcion()
    {
        string rutaLogo = DatosReporteDePrueba.CrearLogo(_directorioTemp, ".png", 200, 60);
        var logger = new LoggerEspia<GeneradorPdfReportes>();
        var generador = new GeneradorPdfReportes(logger);
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 1500, rutaLogo: rutaLogo);
        using var stream = new MemoryStream();

        await generador.GenerarPdfAsync(reporte, stream);
        var paginas = await generador.GenerarPaginasAsync(reporte, 72);

        Assert.True(stream.Length > 0);
        Assert.True(paginas.Count > 1);
        Assert.Empty(logger.Mensajes);
    }

    [Theory]
    [InlineData("corrupto")]
    [InlineData("truncado-cabecera")]
    [InlineData("truncado-mitad")]
    public async Task GenerarPdfAsync_LogoCorruptoOTruncado_GeneraIgualYRegistraAdvertencia(string caso)
    {
        byte[] bytes = File.ReadAllBytes(DatosReporteDePrueba.CrearLogo(_directorioTemp, ".png", 200, 60));
        byte[] danado = caso switch
        {
            "corrupto" => new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 },
            "truncado-cabecera" => bytes.Take(40).ToArray(),
            _ => bytes.Take(bytes.Length / 2).ToArray()
        };
        string ruta = Path.Combine(_directorioTemp, "logo_danado.png");
        File.WriteAllBytes(ruta, danado);

        var logger = new LoggerEspia<GeneradorPdfReportes>();
        var generador = new GeneradorPdfReportes(logger);
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 3, rutaLogo: ruta);
        using var stream = new MemoryStream();

        await generador.GenerarPdfAsync(reporte, stream);

        Assert.True(stream.Length > 0);
        var msg = Assert.Single(logger.Mensajes);
        Assert.Equal(LogLevel.Warning, msg.Nivel);
        Assert.Contains("logo_danado.png", msg.Mensaje);
    }

    [Fact]
    public async Task GenerarPdfAsync_DatosEnLosLimites_MarcaDeCincoClienteMaximoPesoMaximoYTotalesDeVeinteCifras()
    {
        // R25: los valores más anchos que admite el modelo, no solo una dirección larga.
        var generador = new GeneradorPdfReportes(NullLogger<GeneradorPdfReportes>.Instance);
        var baseReporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 3, direccion: new string('D', 150));
        var filas = baseReporte.Filas
            .Select(f => f with
            {
                CodigoGanadero = "WWWWW",
                CodigoCliente = 999999,
                PesoIzquierdo = 600.0m,
                PesoDerecho = 600.0m,
                PesoTotal = 1200.0m,
                TipoDesposte = PesajeFaeneo.Domain.Enums.TipoDesposte.ResCompleta
            })
            .ToList();
        var reporte = baseReporte with
        {
            Filas = filas,
            Totales = new TotalesReporte(3, 3, 0, 12345678901234567890m, 12345678901234567890m, 12345678901234567890m, 12345678901234567890m, 0)
        };
        using var stream = new MemoryStream();

        await generador.GenerarPdfAsync(reporte, stream);
        var paginas = await generador.GenerarPaginasAsync(reporte, 72);

        Assert.True(stream.Length > 0);
        Assert.Single(paginas);
    }

    [Fact]
    public async Task GenerarPdfAsync_AnuladasConAnuladoPorYMotivoDeCienCaracteres_UnaPaginaSinExcepcion()
    {
        var generador = new GeneradorPdfReportes(NullLogger<GeneradorPdfReportes>.Instance);
        var baseReporte = DatosReporteDePrueba.Crear(TipoReporte.Anuladas, 5, motivo: new string('M', 100));
        var reporte = baseReporte with { Filas = baseReporte.Filas.Select(f => f with { AnuladoPor = new string('P', 100) }).ToList() };
        using var stream = new MemoryStream();

        await generador.GenerarPdfAsync(reporte, stream);
        var paginas = await generador.GenerarPaginasAsync(reporte, 72);

        Assert.True(stream.Length > 0);
        Assert.Single(paginas);
    }

    [Fact]
    public async Task GenerarPdfAsync_CaracteresEspecialesEnTodosLosTextos_ConValidacionDeGlifos()
    {
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = true;
        try
        {
            var generador = new GeneradorPdfReportes(NullLogger<GeneradorPdfReportes>.Instance);
            const string especial = "PEÑA JOSÉ N° — … ÁÉÍÓÚ ñ";
            var baseReporte = DatosReporteDePrueba.Crear(TipoReporte.Anuladas, 3, simuladas: 1, motivo: "MOTIVO " + especial);
            var reporte = baseReporte with
            {
                Empresa = "MATADERO " + especial,
                GeneradoPor = "ÑANDÚ " + especial,
                Subtitulo = "Del día — " + especial,
                Filas = baseReporte.Filas.Select(f => f with
                {
                    CodigoGanadero = "Ñ&./",
                    NombreGanadero = especial,
                    NombreCliente = especial,
                    Direccion = especial,
                    AnuladoPor = especial
                }).ToList()
            };
            using var stream = new MemoryStream();

            await generador.GenerarPdfAsync(reporte, stream);

            Assert.True(stream.Length > 0);
        }
        finally
        {
            QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
        }
    }

    [Fact]
    public async Task GenerarPdfAsync_LogoInexistenteOCorrupto_GeneraIgualYRegistraAdvertencia()
    {
        var logger = new LoggerEspia<GeneradorPdfReportes>();
        var generador = new GeneradorPdfReportes(logger);
        string rutaInexistente = Path.Combine(_directorioTemp, "no_existe.png");
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 3, rutaLogo: rutaInexistente);
        using var stream = new MemoryStream();

        await generador.GenerarPdfAsync(reporte, stream);

        Assert.True(stream.Length > 0);
        var msg = Assert.Single(logger.Mensajes);
        Assert.Equal(LogLevel.Warning, msg.Nivel);
        Assert.Contains("no_existe.png", msg.Mensaje);
    }

    [Fact]
    public async Task GenerarPaginasAsync_ConLogo_Pagina1TienePixelesDelColorDelLogo()
    {
        string rutaLogo = DatosReporteDePrueba.CrearLogo(_directorioTemp, ".png", 200, 60, Color.FromArgb(255, 0, 0));
        var generador = new GeneradorPdfReportes(NullLogger<GeneradorPdfReportes>.Instance);

        var conLogo = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 3, rutaLogo: rutaLogo);
        var paginasConLogo = await generador.GenerarPaginasAsync(conLogo, 72);

        var sinLogo = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 3, rutaLogo: null);
        var paginasSinLogo = await generador.GenerarPaginasAsync(sinLogo, 72);

        Assert.NotEmpty(paginasConLogo);
        Assert.NotEmpty(paginasSinLogo);

        static bool EncontrarRojo(byte[] pngBytes)
        {
            using var ms = new MemoryStream(pngBytes);
            using var bmp = new Bitmap(ms);
            int limiteX = (int)(bmp.Width * 0.30);
            int limiteY = (int)(bmp.Height * 0.12);

            for (int y = 0; y < limiteY; y++)
            {
                for (int x = 0; x < limiteX; x++)
                {
                    var pixel = bmp.GetPixel(x, y);
                    if (pixel.R > 200 && pixel.G < 60 && pixel.B < 60)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        Assert.True(EncontrarRojo(paginasConLogo[0]), "La página generada con logo rojo debe contener píxeles rojos en la cabecera.");
        Assert.False(EncontrarRojo(paginasSinLogo[0]), "La página generada sin logo no debe contener píxeles rojos en la cabecera.");
    }

    private sealed class StreamQueAnotaHilo : MemoryStream
    {
        public List<(bool EsPool, int ThreadId)> Invocaciones { get; } = new();

        public override void Write(byte[] buffer, int offset, int count)
        {
            Invocaciones.Add((Thread.CurrentThread.IsThreadPoolThread, Environment.CurrentManagedThreadId));
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Invocaciones.Add((Thread.CurrentThread.IsThreadPoolThread, Environment.CurrentManagedThreadId));
            base.Write(buffer);
        }
    }
}
