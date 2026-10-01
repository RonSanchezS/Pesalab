using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms.Impresion;
using Xunit;

namespace PesajeFaeneo.Tests.Impresion;

public class DocumentoPaginasReporteTests
{
    private static byte[] CrearPngNegro(int ancho = 170, int alto = 220)
    {
        using var bmp = new Bitmap(ancho, alto);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Black);
        }
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    [Fact]
    public void CalcularDestino_HojaCartaEImagenProporcional_OcupaTodaLaHoja()
    {
        var pagina = new SizeF(850, 1100);
        var imagen = new Size(1700, 2200);

        var destino = DocumentoPaginasReporte.CalcularDestino(pagina, imagen, 0f, 0f);

        Assert.Equal(0f, destino.X);
        Assert.Equal(0f, destino.Y);
        Assert.Equal(850f, destino.Width);
        Assert.Equal(1100f, destino.Height);
    }

    [Fact]
    public void CalcularDestino_HojaA4ConImagenCarta_CentraVerticalmenteSinDeformar()
    {
        var pagina = new SizeF(827, 1169);
        var imagen = new Size(1700, 2200);

        var destino = DocumentoPaginasReporte.CalcularDestino(pagina, imagen, 0f, 0f);

        Assert.Equal(0f, destino.X);
        Assert.Equal(827f, destino.Width);
        // Escala = 827 / 1700 ≈ 0.48647; Alto = 2200 * 0.48647 ≈ 1070.235; Y = (1169 - 1070.235) / 2 ≈ 49.38
        Assert.InRange(destino.Height, 1069.7f, 1070.7f);
        Assert.InRange(destino.Y, 48.9f, 49.9f);
    }

    [Fact]
    public void CalcularDestino_MargenesFisicos_SeRestanDeCoordenadasXY()
    {
        var pagina = new SizeF(850, 1100);
        var imagen = new Size(1700, 2200);

        var destino = DocumentoPaginasReporte.CalcularDestino(pagina, imagen, 25f, 20f);

        Assert.Equal(-25f, destino.X);
        Assert.Equal(-20f, destino.Y);
        Assert.Equal(850f, destino.Width);
        Assert.Equal(1100f, destino.Height);
    }

    [Fact]
    public void ProcesarPagina_TresPaginas_AvanzaSecuencialmenteYReiniciaConSimularInicio()
    {
        HiloSta.Ejecutar(() =>
        {
            var p1 = CrearPngNegro();
            var p2 = CrearPngNegro();
            var p3 = CrearPngNegro();
            using var doc = new DocumentoPaginasReporte([p1, p2, p3], false, "Reporte Test");

            using var bmp = new Bitmap(100, 100);
            using var g = Graphics.FromImage(bmp);
            var tamano = new SizeF(100, 100);

            // Página 1
            bool hayMas1 = doc.ProcesarPagina(g, tamano, 0, 0);
            Assert.True(hayMas1);

            // Página 2
            bool hayMas2 = doc.ProcesarPagina(g, tamano, 0, 0);
            Assert.True(hayMas2);

            // Página 3 (última)
            bool hayMas3 = doc.ProcesarPagina(g, tamano, 0, 0);
            Assert.False(hayMas3);

            // Reiniciar con SimularInicioParaTest
            doc.SimularInicioParaTest();
            bool hayMasTrasReinicio = doc.ProcesarPagina(g, tamano, 0, 0);
            Assert.True(hayMasTrasReinicio);
        });
    }

    [Fact]
    public void ProcesarPagina_DibujaContenidoSobreGraphics()
    {
        HiloSta.Ejecutar(() =>
        {
            var paginaNegra = CrearPngNegro(100, 100);
            using var doc = new DocumentoPaginasReporte([paginaNegra], false, "Reporte");

            using var lienzo = new Bitmap(100, 100);
            using (var g = Graphics.FromImage(lienzo))
            {
                g.Clear(Color.White);
                doc.ProcesarPagina(g, new SizeF(100, 100), 0, 0);
            }

            // El centro debe haberse pintado de negro (R,G,B < 30)
            var pixelCentro = lienzo.GetPixel(50, 50);
            Assert.True(pixelCentro.R < 30 && pixelCentro.G < 30 && pixelCentro.B < 30, "El centro debe haberse dibujado con el PNG negro.");
        });
    }

    [Fact]
    public void Constructor_ValidacionesDeArgumentos()
    {
        HiloSta.Ejecutar(() =>
        {
            Assert.Throws<ArgumentNullException>(() => new DocumentoPaginasReporte(null!, false, "Test"));
            Assert.Throws<ArgumentException>(() => new DocumentoPaginasReporte(Array.Empty<byte[]>(), false, "Test"));
        });
    }

    [Fact]
    public void Constructor_ConfiguraValoresPorDefecto()
    {
        HiloSta.Ejecutar(() =>
        {
            var p1 = CrearPngNegro();
            using var doc1 = new DocumentoPaginasReporte([p1], false, "");
            Assert.IsType<StandardPrintController>(doc1.PrintController);
            Assert.False(doc1.OriginAtMargins);
            Assert.Equal("Reporte", doc1.DocumentName);

            using var doc2 = new DocumentoPaginasReporte([p1], true, "Reporte Personalizado");
            Assert.Equal("Reporte Personalizado", doc2.DocumentName);
        });
    }
}
