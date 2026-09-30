using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Extensions.Logging.Abstractions;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms.Impresion;
using Xunit;

namespace PesajeFaeneo.Tests.Impresion;

public class CargadorLogoTests
{
    [Fact]
    public void Cargar_RutaNulaOInexistente_DevuelveNull()
    {
        HiloSta.Ejecutar(() =>
        {
            var logger = NullLogger.Instance;

            Assert.Null(CargadorLogo.Cargar(null, logger));
            Assert.Null(CargadorLogo.Cargar(string.Empty, logger));
            Assert.Null(CargadorLogo.Cargar(@"C:\ruta_inexistente_12345.png", logger));
        });
    }

    [Fact]
    public void Cargar_ArchivoDanado_DevuelveNullSinLanzar()
    {
        HiloSta.Ejecutar(() =>
        {
            var logger = NullLogger.Instance;
            var dirTemp = Path.Combine(Path.GetTempPath(), "PesajeTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dirTemp);

            try
            {
                var rutaDanada = Path.Combine(dirTemp, "corrupto.png");
                File.WriteAllBytes(rutaDanada, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

                var resultado = CargadorLogo.Cargar(rutaDanada, logger);
                Assert.Null(resultado);
            }
            finally
            {
                Directory.Delete(dirTemp, true);
            }
        });
    }

    [Fact]
    public void Cargar_PngValido_DevuelveImagenYNoBloqueaArchivo()
    {
        HiloSta.Ejecutar(() =>
        {
            var logger = NullLogger.Instance;
            var dirTemp = Path.Combine(Path.GetTempPath(), "PesajeTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dirTemp);

            try
            {
                var rutaPng = Path.Combine(dirTemp, "logo_valido.png");
                using (var bmp = new Bitmap(10, 10))
                {
                    bmp.Save(rutaPng, ImageFormat.Png);
                }

                Image? imagenCargada = null;
                try
                {
                    imagenCargada = CargadorLogo.Cargar(rutaPng, logger);
                    Assert.NotNull(imagenCargada);
                    Assert.Equal(10, imagenCargada.Width);
                    Assert.Equal(10, imagenCargada.Height);

                    // Verificar que el archivo no quedó bloqueado: debe poder eliminarse
                    File.Delete(rutaPng);
                    Assert.False(File.Exists(rutaPng));
                }
                finally
                {
                    imagenCargada?.Dispose();
                }
            }
            finally
            {
                if (Directory.Exists(dirTemp))
                {
                    Directory.Delete(dirTemp, true);
                }
            }
        });
    }
}
