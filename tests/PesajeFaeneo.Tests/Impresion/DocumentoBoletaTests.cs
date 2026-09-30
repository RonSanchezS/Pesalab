using System.Drawing;
using System.Drawing.Printing;
using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms.Impresion;
using Xunit;

namespace PesajeFaeneo.Tests.Impresion;

public class DocumentoBoletaTests
{
    [Fact]
    public void Constructor_ConfiguraStandardPrintControllerYOriginAtMargins()
    {
        HiloSta.Ejecutar(() =>
        {
            using var doc = new DocumentoBoleta(_ => new PaginaBoleta(100f, 100f, Array.Empty<ElementoBoleta>()), 1, null, 0f, 0f, false, "Test");

            Assert.IsType<StandardPrintController>(doc.PrintController);
            Assert.False(doc.OriginAtMargins);
            Assert.Equal("Test", doc.DocumentName);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void Constructor_TotalCopiasInvalido_LanzaArgumentOutOfRangeException(int copias)
    {
        HiloSta.Ejecutar(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new DocumentoBoleta(_ => new PaginaBoleta(100f, 100f, Array.Empty<ElementoBoleta>()), copias, null, 0f, 0f, false, "Test"));
        });
    }

    [Fact]
    public void Dispose_DesechaLogo()
    {
        HiloSta.Ejecutar(() =>
        {
            var logo = new Bitmap(10, 10);
            var doc = new DocumentoBoleta(_ => new PaginaBoleta(100f, 100f, Array.Empty<ElementoBoleta>()), 1, logo, 0f, 0f, false, "Test");

            doc.Dispose();

            // Tras Dispose, acceder a propiedades de logo debe lanzar ArgumentException
            Assert.Throws<ArgumentException>(() => _ = logo.Width);
        });
    }

    [Fact]
    public void ContadorCopias_SimularInicio_ReiniciaContadorYEntregaCopiasCorrectas()
    {
        HiloSta.Ejecutar(() =>
        {
            var copiasRecibidas = new List<int>();
            using var doc = new DocumentoBoleta(c =>
            {
                copiasRecibidas.Add(c);
                return new PaginaBoleta(100f, 100f, Array.Empty<ElementoBoleta>());
            }, 2, null, 0f, 0f, false, "Test");

            using var bmp = new Bitmap(900, 600);
            using var g = Graphics.FromImage(bmp);

            // Primera pasada
            doc.SimularInicioParaTest();
            var hayMas1 = doc.ProcesarPagina(g, 0f, 0f);
            var hayMas2 = doc.ProcesarPagina(g, 0f, 0f);

            Assert.True(hayMas1);
            Assert.False(hayMas2);

            // Segunda pasada (simula nueva impresión sobre el mismo documento)
            doc.SimularInicioParaTest();
            var hayMas3 = doc.ProcesarPagina(g, 0f, 0f);
            var hayMas4 = doc.ProcesarPagina(g, 0f, 0f);

            Assert.True(hayMas3);
            Assert.False(hayMas4);

            Assert.Equal(new[] { 1, 2, 1, 2 }, copiasRecibidas);
        });
    }

    [Fact]
    public void CompensacionMargenFisico_RestaMargenesAlPintar()
    {
        HiloSta.Ejecutar(() =>
        {
            using var bmp = new Bitmap(600, 600);
            using (var gInit = Graphics.FromImage(bmp))
            {
                gInit.Clear(Color.White);
            }

            // Rectángulo de 10x10 a 15x15 en mm
            var elemento = new RectanguloBoleta(10f, 10f, 5f, 5f, 1f);
            var pagina = new PaginaBoleta(100f, 100f, new[] { elemento });

            using var doc = new DocumentoBoleta(_ => pagina, 1, null, 0f, 0f, false, "Test");

            using (var g = Graphics.FromImage(bmp))
            {
                // Margen físico de 4 mm en X y 3 mm en Y -> origen queda en (-4, -3) mm
                // El rectángulo (10, 10) debe dibujarse en (6, 7) mm
                doc.SimularInicioParaTest();
                doc.ProcesarPagina(g, 4f, 3f);
            }

            int ConvertirMmAPxX(float mm) => (int)Math.Round(mm * bmp.HorizontalResolution / 25.4f);
            int ConvertirMmAPxY(float mm) => (int)Math.Round(mm * bmp.VerticalResolution / 25.4f);

            // Píxel cerca de (6 mm, 7 mm) debe tener tinta oscura
            int xDespl = ConvertirMmAPxX(6f);
            int yDespl = ConvertirMmAPxY(7f);

            bool hayTintaEnDesplazado = false;
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    int px = Math.Clamp(xDespl + dx, 0, bmp.Width - 1);
                    int py = Math.Clamp(yDespl + dy, 0, bmp.Height - 1);
                    var c = bmp.GetPixel(px, py);
                    if (c.R < 200 || c.G < 200 || c.B < 200)
                    {
                        hayTintaEnDesplazado = true;
                        break;
                    }
                }
                if (hayTintaEnDesplazado) break;
            }

            Assert.True(hayTintaEnDesplazado, "Debió encontrarse tinta cerca de (6 mm, 7 mm) por compensación de margen físico.");

            // Píxel en (10 mm, 6 mm) o (2 mm, 2 mm) debe ser blanco
            var pixelBlanco = bmp.GetPixel(ConvertirMmAPxX(2f), ConvertirMmAPxY(2f));
            Assert.Equal(255, pixelBlanco.R);
            Assert.Equal(255, pixelBlanco.G);
            Assert.Equal(255, pixelBlanco.B);
        });
    }
}
