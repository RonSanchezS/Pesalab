using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PesajeFaeneo.Application.Reportes.Dtos;
using PesajeFaeneo.Infrastructure.Reportes;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Reportes;
using Xunit;

namespace PesajeFaeneo.Tests;

public class GeneradorExcelReportesTests : IDisposable
{
    private readonly string _directorioTemp;

    public GeneradorExcelReportesTests()
    {
        _directorioTemp = Path.Combine(Path.GetTempPath(), "pesalab-f7-excel-" + Guid.NewGuid().ToString("N"));
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
    public async Task GenerarExcelAsync_ReporteNormal_GeneraLibroValidoConHojaPesajes()
    {
        var generador = new GeneradorExcelReportes(NullLogger<GeneradorExcelReportes>.Instance);
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 5);
        using var stream = new MemoryStream();

        await generador.GenerarExcelAsync(reporte, stream);

        Assert.True(stream.Length > 0);
        stream.Position = 0;
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet("Pesajes");
        Assert.NotNull(ws);

        // Fila 1 a 4 encabezados
        Assert.Equal(reporte.Empresa, ws.Cell(1, 1).GetString());
        Assert.Equal(reporte.Titulo, ws.Cell(2, 1).GetString());
        Assert.Equal(reporte.Subtitulo, ws.Cell(3, 1).GetString());
        Assert.Contains(reporte.GeneradoPor, ws.Cell(4, 1).GetString());

        // Fila 6: 14 columnas
        Assert.Equal("Item", ws.Cell(6, 1).GetString());
        Assert.Equal("N° boleta", ws.Cell(6, 2).GetString());
        Assert.Equal("Fecha", ws.Cell(6, 3).GetString());
        Assert.Equal("N° res (secuencia)", ws.Cell(6, 4).GetString());
        Assert.Equal("Código", ws.Cell(6, 5).GetString());
        Assert.Equal("Ganadero", ws.Cell(6, 6).GetString());
        Assert.Equal("Cliente (código)", ws.Cell(6, 7).GetString());
        Assert.Equal("Nombre del cliente", ws.Cell(6, 8).GetString());
        Assert.Equal("Fletero", ws.Cell(6, 9).GetString());
        Assert.Equal("Dirección", ws.Cell(6, 10).GetString());
        Assert.Equal("Izq. (kg)", ws.Cell(6, 11).GetString());
        Assert.Equal("Der. (kg)", ws.Cell(6, 12).GetString());
        Assert.Equal("Total neto (kg)", ws.Cell(6, 13).GetString());
        Assert.Equal("Tipo corte", ws.Cell(6, 14).GetString());
        Assert.Equal("Origen", ws.Cell(6, 15).GetString());

        // Fila 7: datos tipados
        Assert.Equal(XLDataType.Number, ws.Cell(7, 1).DataType);
        Assert.Equal(1, ws.Cell(7, 1).GetValue<int>());
        Assert.Equal(XLDataType.Number, ws.Cell(7, 2).DataType);
        Assert.Equal(1001, ws.Cell(7, 2).GetValue<long>());
        Assert.Equal(XLDataType.DateTime, ws.Cell(7, 3).DataType);
        Assert.Equal(XLDataType.Number, ws.Cell(7, 4).DataType);
        Assert.Equal(1, ws.Cell(7, 4).GetValue<int>());
        Assert.Equal(XLDataType.Number, ws.Cell(7, 11).DataType);
        Assert.Equal(XLDataType.Number, ws.Cell(7, 12).DataType);
        Assert.Equal(XLDataType.Number, ws.Cell(7, 13).DataType);
        Assert.False(ws.Cell(7, 13).HasFormula);

        // Formatos de celdas
        Assert.Equal("#,##0.0", ws.Cell(7, 11).Style.NumberFormat.Format);
        Assert.Equal("#,##0.0", ws.Cell(7, 13).Style.NumberFormat.Format);

        // Totales: u = 6 + 5 = 11; última fila = 11 + 5 = 16
        int u = 11;
        Assert.Equal("Total peso neto (kg)", ws.Cell(u + 2, 1).GetString());
        Assert.Equal(reporte.Totales.TotalNetoKg, ws.Cell(u + 2, 13).GetValue<decimal>());
        Assert.Equal("#,##0.0", ws.Cell(u + 2, 13).Style.NumberFormat.Format);

        Assert.Equal("Cantidad de reses", ws.Cell(u + 3, 1).GetString());
        Assert.Equal(reporte.Totales.Reses, ws.Cell(u + 3, 13).GetValue<decimal>());
        Assert.Equal("#,##0.0", ws.Cell(u + 3, 13).Style.NumberFormat.Format);

        Assert.Equal("Boletas", ws.Cell(u + 4, 1).GetString());
        Assert.Equal(5, ws.Cell(u + 4, 13).GetValue<int>());

        Assert.Equal("Res completa / media res", ws.Cell(u + 5, 1).GetString());
        Assert.Equal(u + 5, ws.LastRowUsed()!.RowNumber());
    }

    [Fact]
    public async Task GenerarExcelAsync_ReporteAnuladas_GeneraLibroConHojaAnuladasYTotalesAnuladas()
    {
        var generador = new GeneradorExcelReportes(NullLogger<GeneradorExcelReportes>.Instance);
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.Anuladas, 4);
        using var stream = new MemoryStream();

        await generador.GenerarExcelAsync(reporte, stream);

        stream.Position = 0;
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet("Anuladas");
        Assert.NotNull(ws);

        // Fila 6: 13 columnas
        Assert.Equal("Item", ws.Cell(6, 1).GetString());
        Assert.Equal("N° boleta", ws.Cell(6, 2).GetString());
        Assert.Equal("Fecha", ws.Cell(6, 3).GetString());
        Assert.Equal("Código", ws.Cell(6, 4).GetString());
        Assert.Equal("Ganadero", ws.Cell(6, 5).GetString());
        Assert.Equal("Cliente (código)", ws.Cell(6, 6).GetString());
        Assert.Equal("Nombre del cliente", ws.Cell(6, 7).GetString());
        Assert.Equal("Tipo corte", ws.Cell(6, 8).GetString());
        Assert.Equal("Total neto (kg)", ws.Cell(6, 9).GetString());
        Assert.Equal("Anulada el", ws.Cell(6, 10).GetString());
        Assert.Equal("Anulada por", ws.Cell(6, 11).GetString());
        Assert.Equal("Motivo", ws.Cell(6, 12).GetString());
        Assert.Equal("Origen", ws.Cell(6, 13).GetString());

        // Fila 7: datos anuladas
        Assert.Equal(XLDataType.DateTime, ws.Cell(7, 10).DataType);
        Assert.Equal("ADMIN", ws.Cell(7, 11).GetString());
        Assert.Equal("ERROR EN DATOS", ws.Cell(7, 12).GetString());

        // Totales: u = 6 + 4 = 10; última fila = 10 + 3 = 13
        int u = 10;
        Assert.Equal("Boletas anuladas", ws.Cell(u + 2, 1).GetString());
        Assert.Equal(4, ws.Cell(u + 2, 9).GetValue<int>());

        Assert.Equal("Peso neto anulado (kg)", ws.Cell(u + 3, 1).GetString());
        Assert.Equal(reporte.Totales.TotalNetoKg, ws.Cell(u + 3, 9).GetValue<decimal>());
        Assert.Equal("#,##0.0", ws.Cell(u + 3, 9).Style.NumberFormat.Format);

        Assert.Equal(u + 3, ws.LastRowUsed()!.RowNumber());
    }

    [Fact]
    public async Task GenerarExcelAsync_ReporteVacio_MuestraSinBoletasYTotalesEnFilaAlineada()
    {
        var generador = new GeneradorExcelReportes(NullLogger<GeneradorExcelReportes>.Instance);
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 0);
        using var stream = new MemoryStream();

        await generador.GenerarExcelAsync(reporte, stream);

        stream.Position = 0;
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet("Pesajes");

        // Fila 7: mensaje sin boletas
        Assert.Equal(PesajeFaeneo.Application.Reportes.TextosReporte.SinBoletas, ws.Cell(7, 1).GetString());
        Assert.True(ws.Cell(7, 1).Style.Font.Italic);

        // u = 7 -> última fila u + 5 = 12
        Assert.Equal(12, ws.LastRowUsed()!.RowNumber());
        Assert.Equal("Total peso neto (kg)", ws.Cell(9, 1).GetString());
        Assert.Equal(0.0m, ws.Cell(9, 13).GetValue<decimal>());
    }

    [Fact]
    public async Task GenerarExcelAsync_ConSimuladas_AgregaNotaEnUltimaFila()
    {
        var generador = new GeneradorExcelReportes(NullLogger<GeneradorExcelReportes>.Instance);
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 5, simuladas: 2);
        using var stream = new MemoryStream();

        await generador.GenerarExcelAsync(reporte, stream);

        stream.Position = 0;
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet("Pesajes");

        // u = 11 -> totales hasta 16 -> nota en fila 17
        int ultimaFila = ws.LastRowUsed()!.RowNumber();
        Assert.Equal(17, ultimaFila);
        Assert.Contains("SIMULADO", ws.Cell(17, 1).GetString());
        Assert.True(ws.Cell(17, 1).Style.Font.Italic);
    }

    [Fact]
    public async Task GenerarExcelAsync_TextoQueComienzaConSignoIgual_SeGuardaComoTextoSinFormula()
    {
        var generador = new GeneradorExcelReportes(NullLogger<GeneradorExcelReportes>.Instance);
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 1, direccion: "=1+1");
        using var stream = new MemoryStream();

        await generador.GenerarExcelAsync(reporte, stream);

        stream.Position = 0;
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet("Pesajes");

        var celdaDireccion = ws.Cell(7, 10);
        Assert.False(celdaDireccion.HasFormula);
        Assert.Equal("=1+1", celdaDireccion.GetString());
    }

    [Fact]
    public async Task GenerarExcelAsync_ConLogoValido_InsertaImagenEnCeldaA1()
    {
        string rutaLogo = DatosReporteDePrueba.CrearLogo(_directorioTemp, ".png", 200, 60);
        var generador = new GeneradorExcelReportes(NullLogger<GeneradorExcelReportes>.Instance);
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 3, rutaLogo: rutaLogo);
        using var stream = new MemoryStream();

        await generador.GenerarExcelAsync(reporte, stream);

        stream.Position = 0;
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet("Pesajes");

        Assert.Equal(1, ws.Pictures.Count);
        Assert.Equal(36, ws.Row(1).Height);
    }

    [Fact]
    public async Task GenerarExcelAsync_ConLogoInexistente_GeneraExcelIgualSinImagenesYRegistraAdvertencia()
    {
        var logger = new LoggerEspia<GeneradorExcelReportes>();
        var generador = new GeneradorExcelReportes(logger);
        string rutaInexistente = Path.Combine(_directorioTemp, "inexistente.png");
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 3, rutaLogo: rutaInexistente);
        using var stream = new MemoryStream();

        await generador.GenerarExcelAsync(reporte, stream);

        stream.Position = 0;
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet("Pesajes");

        Assert.Equal(0, ws.Pictures.Count);
        var msg = Assert.Single(logger.Mensajes);
        Assert.Equal(LogLevel.Warning, msg.Nivel);
        Assert.Contains("inexistente.png", msg.Mensaje);
    }

    [Fact]
    public async Task GenerarExcelAsync_ConfiguracionDePaginaYCongelamientoDePaneles()
    {
        var generador = new GeneradorExcelReportes(NullLogger<GeneradorExcelReportes>.Instance);
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 2);
        using var stream = new MemoryStream();

        await generador.GenerarExcelAsync(reporte, stream);

        stream.Position = 0;
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet("Pesajes");

        Assert.Equal(XLPageOrientation.Portrait, ws.PageSetup.PageOrientation);
        Assert.Equal(XLPaperSize.LetterPaper, ws.PageSetup.PaperSize);
        Assert.Equal(1, ws.PageSetup.PagesWide);
        Assert.Equal(0, ws.PageSetup.PagesTall);
    }

    private static async Task<XLWorkbook> GenerarLibroAsync(ReportePesajes reporte, ILogger<GeneradorExcelReportes>? logger = null)
    {
        var generador = new GeneradorExcelReportes(logger ?? NullLogger<GeneradorExcelReportes>.Instance);
        using var stream = new MemoryStream();
        await generador.GenerarExcelAsync(reporte, stream);
        stream.Position = 0;
        return new XLWorkbook(stream);
    }

    private static ReportePesajes ConFilas(ReportePesajes baseReporte, List<FilaReporte> filas) =>
        baseReporte with { Filas = filas, Totales = PesajeFaeneo.Application.Reportes.CalculadoraTotalesReporte.Calcular(filas) };

    [Fact]
    public async Task GenerarExcelAsync_Encabezado_TieneAlMenosCuatroRangosCombinados()
    {
        using var wb = await GenerarLibroAsync(DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 3));

        Assert.True(wb.Worksheet("Pesajes").MergedRanges.Count >= 4);
    }

    [Fact]
    public async Task GenerarExcelAsync_CasoLegado_OchoResCompletaYTresMediaRes_TotalesDeLaHu()
    {
        // 8 res completa (109,0 + 106,0) + 3 media res (106,0): total 2.038,0 y 9,5 reses (media res = 0,5).
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 11);
        using var wb = await GenerarLibroAsync(reporte);
        var ws = wb.Worksheet("Pesajes");

        int u = 6 + 11;
        Assert.Equal(2038.0m, ws.Cell(u + 2, 13).GetValue<decimal>());
        Assert.Equal(9.5m, ws.Cell(u + 3, 13).GetValue<decimal>());
        Assert.Equal(11, ws.Cell(u + 4, 13).GetValue<int>());
        Assert.Equal("8 / 3", ws.Cell(u + 5, 13).GetString());
    }

    [Fact]
    public async Task GenerarExcelAsync_PesosConDecimales_SeEscribenExactosYLaFechaTieneSuFormato()
    {
        var baseReporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 1);
        var fila = baseReporte.Filas[0] with { PesoIzquierdo = 109.3m, PesoDerecho = 106.7m, PesoTotal = 216.0m };
        using var wb = await GenerarLibroAsync(ConFilas(baseReporte, new List<FilaReporte> { fila }));
        var ws = wb.Worksheet("Pesajes");

        Assert.Equal(109.3m, ws.Cell(7, 11).GetValue<decimal>());
        Assert.Equal(106.7m, ws.Cell(7, 12).GetValue<decimal>());
        Assert.Equal(216.0m, ws.Cell(7, 13).GetValue<decimal>());
        Assert.Equal(216.0m, ws.Cell(7 + 1 + 1, 13).GetValue<decimal>());   // total neto en la fila u + 2
        Assert.Equal("dd/MM/yyyy HH:mm", ws.Cell(7, 3).Style.DateFormat.Format);
        Assert.Equal(fila.FechaHoraLocal, ws.Cell(7, 3).GetDateTime());
    }

    [Fact]
    public async Task GenerarExcelAsync_Anuladas_AnuladaPorYMotivoDeCienCaracteresCompletos()
    {
        string motivo100 = new('M', 100);
        string por100 = new('P', 100);
        var baseReporte = DatosReporteDePrueba.Crear(TipoReporte.Anuladas, 3, motivo: motivo100);
        var filas = baseReporte.Filas.Select(f => f with { AnuladoPor = por100 }).ToList();
        using var wb = await GenerarLibroAsync(ConFilas(baseReporte, filas));
        var ws = wb.Worksheet("Anuladas");

        Assert.Equal(por100, ws.Cell(7, 11).GetString());
        Assert.Equal(motivo100, ws.Cell(7, 12).GetString());
        Assert.Equal(6 + 3 + 3, ws.LastRowUsed()!.RowNumber());
    }

    [Fact]
    public async Task GenerarExcelAsync_InyeccionDeFormulas_QuedaComoTextoEnNormalYAnuladas()
    {
        const string hiperenlace = "=HYPERLINK(\"http://x\")";
        using var wbNormal = await GenerarLibroAsync(DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 1, direccion: hiperenlace));
        var dir = wbNormal.Worksheet("Pesajes").Cell(7, 10);
        Assert.False(dir.HasFormula);
        Assert.Equal(hiperenlace, dir.GetString());

        using var wbAnuladas = await GenerarLibroAsync(DatosReporteDePrueba.Crear(TipoReporte.Anuladas, 1, motivo: "=1+1"));
        var motivo = wbAnuladas.Worksheet("Anuladas").Cell(7, 12);
        Assert.False(motivo.HasFormula);
        Assert.Equal("=1+1", motivo.GetString());
    }

    [Fact]
    public async Task GenerarExcelAsync_Simuladas_OrigenSimuladoYNumeroDeBoletaNumericoSinAsterisco()
    {
        using var wb = await GenerarLibroAsync(DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 3, simuladas: 1));
        var ws = wb.Worksheet("Pesajes");

        Assert.Equal("SIMULADO", ws.Cell(7, 15).GetString());
        Assert.Equal("BALANZA", ws.Cell(8, 15).GetString());
        Assert.Equal(XLDataType.Number, ws.Cell(7, 2).DataType);
        Assert.Equal(1001, ws.Cell(7, 2).GetValue<long>());
    }

    [Fact]
    public async Task GenerarExcelAsync_VeinteMilFilas_UltimaFilaYSinFormulas()
    {
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 20_000);
        using var wb = await GenerarLibroAsync(reporte);
        var ws = wb.Worksheet("Pesajes");

        Assert.Equal(6 + 20_000 + 5, ws.LastRowUsed()!.RowNumber());
        Assert.Equal(reporte.Totales.TotalNetoKg, ws.Cell(6 + 20_000 + 2, 13).GetValue<decimal>());
        Assert.False(ws.Cell(6 + 20_000 + 2, 13).HasFormula);
    }

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".bmp")]
    public async Task GenerarExcelAsync_ConLogoJpegOBmp_InsertaUnaImagen(string extension)
    {
        string rutaLogo = DatosReporteDePrueba.CrearLogo(_directorioTemp, extension, 200, 60);
        var logger = new LoggerEspia<GeneradorExcelReportes>();
        using var wb = await GenerarLibroAsync(DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 2, rutaLogo: rutaLogo), logger);

        Assert.Equal(1, wb.Worksheet("Pesajes").Pictures.Count);
        Assert.Empty(logger.Mensajes);
    }

    [Fact]
    public async Task GenerarExcelAsync_SinLogoConfigurado_NoInsertaImagenNiAdvierte()
    {
        var logger = new LoggerEspia<GeneradorExcelReportes>();
        using var wb = await GenerarLibroAsync(DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 2, rutaLogo: null), logger);

        Assert.Equal(0, wb.Worksheet("Pesajes").Pictures.Count);
        Assert.Empty(logger.Mensajes);
    }

    [Theory]
    [InlineData("corrupto")]
    [InlineData("truncado-cabecera")]
    [InlineData("truncado-mitad")]
    public async Task GenerarExcelAsync_ConLogoDanado_GeneraLibroSinImagenYAdvierte(string caso)
    {
        // R62: un logo cortado jamás debe llegar al libro (ClosedXML solo valida la cabecera).
        string rutaBuena = DatosReporteDePrueba.CrearLogo(_directorioTemp, ".png", 200, 60);
        byte[] bytes = File.ReadAllBytes(rutaBuena);
        byte[] danado = caso switch
        {
            "corrupto" => new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
            "truncado-cabecera" => bytes.Take(40).ToArray(),
            _ => bytes.Take(bytes.Length / 2).ToArray()
        };
        string ruta = Path.Combine(_directorioTemp, "danado.png");
        File.WriteAllBytes(ruta, danado);

        var logger = new LoggerEspia<GeneradorExcelReportes>();
        using var wb = await GenerarLibroAsync(DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 2, rutaLogo: ruta), logger);

        Assert.Equal(0, wb.Worksheet("Pesajes").Pictures.Count);
        var msg = Assert.Single(logger.Mensajes);
        Assert.Equal(LogLevel.Warning, msg.Nivel);
        Assert.Contains("danado.png", msg.Mensaje);
    }

    [Fact]
    public async Task GenerarExcelAsync_FijaPanelesYEncabezadoDeImpresion()
    {
        using var wb = await GenerarLibroAsync(DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 2));
        var ws = wb.Worksheet("Pesajes");

        Assert.Equal(6, ws.SheetView.SplitRow);
        Assert.Equal(6, ws.PageSetup.FirstRowToRepeatAtTop);
        Assert.Equal(6, ws.PageSetup.LastRowToRepeatAtTop);
    }

    [Fact]
    public async Task GeneradorExcelReportes_ValidacionArgumentos()
    {
        var generador = new GeneradorExcelReportes(NullLogger<GeneradorExcelReportes>.Instance);
        var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 2);

        await Assert.ThrowsAsync<ArgumentNullException>(() => generador.GenerarExcelAsync(null!, new MemoryStream()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => generador.GenerarExcelAsync(reporte, null!));
        Assert.Throws<ArgumentNullException>(() => new GeneradorExcelReportes(null!));
    }

    [Fact]
    public void GenerarExcelAsync_NoCorreEnHiloQueLlama_EjecutaEnThreadPool()
    {
        HiloSta.Ejecutar(() =>
        {
            int staThreadId = Environment.CurrentManagedThreadId;
            var generador = new GeneradorExcelReportes(NullLogger<GeneradorExcelReportes>.Instance);
            var reporte = DatosReporteDePrueba.Crear(TipoReporte.PesajesEntreFechas, 3);
            using var stream = new StreamQueAnotaHilo();

            generador.GenerarExcelAsync(reporte, stream).GetAwaiter().GetResult();

            Assert.NotEmpty(stream.Invocaciones);
            Assert.All(stream.Invocaciones, inv =>
            {
                Assert.True(inv.EsPool, "La escritura de Excel debió realizarse en un hilo del ThreadPool.");
                Assert.NotEqual(staThreadId, inv.ThreadId);
            });
        });
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
