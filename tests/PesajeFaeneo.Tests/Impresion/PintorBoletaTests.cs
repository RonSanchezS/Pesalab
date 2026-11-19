using System.Drawing;
using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.WinForms.Impresion;
using Xunit;

namespace PesajeFaeneo.Tests.Impresion;

public class PintorBoletaTests
{
    private static DatosBoleta CrearDatosPrueba(string? rutaLogo = null)
    {
        return new DatosBoleta(
            PesajeId: 1,
            NumeroBoleta: 100,
            FechaHoraLocal: new DateTime(2026, 9, 26, 22, 30, 0, DateTimeKind.Unspecified),
            Empresa: new EmpresaBoleta("EMPRESA TEST", "CALLE 1", "CIUDAD", "70000000", "REG-1", "80000000", rutaLogo),
            CodigoGanadero: "AH",
            NombreGanadero: "GANADERO TEST",
            Secuencia: 1,
            CodigoCliente: 10,
            NombreCliente: "CLIENTE TEST",
            Direccion: "DIRECCION TEST",
            ColorRaza: "NEGRO",
            Sexo: 'M',
            TipoDesposte: TipoDesposte.ResCompleta,
            PesoIzquierdo: 120.0m,
            PesoDerecho: 118.5m,
            PesoTotal: 238.5m,
            Observacion: "OBSERVACION DE PRUEBA",
            Origen: OrigenPeso.Simulador,
            Estado: EstadoPesaje.Vigente,
            MotivoAnulacion: null,
            NombreOperador: "OPERADOR TEST",
            Fletero: new FleteroBoleta(1, "FLETERO 1", "70000001"),
            ImpresionesPrevias: 1,
            GeneradoEnLocal: new DateTime(2026, 9, 26, 22, 30, 0, DateTimeKind.Unspecified),
            EsMuestra: false);
    }

    [Fact]
    public void Pintar_MediaCartaYTicket_NoLanzaConYSinLogo()
    {
        HiloSta.Ejecutar(() =>
        {
            var datos = CrearDatosPrueba();
            var pagMediaCarta = DisenoBoleta.Crear(datos, FormatoBoleta.MediaCarta, 1, 1);
            var pagTicket = DisenoBoleta.Crear(datos, FormatoBoleta.Ticket80mm, 1, 1);

            using var bmp = new Bitmap(900, 600);
            using var g = Graphics.FromImage(bmp);
            using var logo = new Bitmap(50, 20);

            // Sin logo
            PintorBoleta.Pintar(g, pagMediaCarta, null, 0f, 0f);
            PintorBoleta.Pintar(g, pagTicket, null, 0f, 0f);

            // Con logo
            PintorBoleta.Pintar(g, pagMediaCarta, logo, 0f, 0f);
            PintorBoleta.Pintar(g, pagTicket, logo, 0f, 0f);
        });
    }

    [Fact]
    public void Pintar_RestauraPageUnitYEstadoDeGraphics()
    {
        HiloSta.Ejecutar(() =>
        {
            var datos = CrearDatosPrueba();
            var pagina = DisenoBoleta.Crear(datos, FormatoBoleta.MediaCarta, 1, 1);

            using var bmp = new Bitmap(900, 600);
            using var g = Graphics.FromImage(bmp);
            g.PageUnit = GraphicsUnit.Pixel;

            PintorBoleta.Pintar(g, pagina, null, 10f, 10f);

            Assert.Equal(GraphicsUnit.Pixel, g.PageUnit);
        });
    }

    [Fact]
    public void Pintar_TextoBoletaNegro_DejaPixelesNoBlancos()
    {
        HiloSta.Ejecutar(() =>
        {
            using var bmp = new Bitmap(400, 200);
            using (var gInit = Graphics.FromImage(bmp))
            {
                gInit.Clear(Color.White);
            }

            var elementos = new List<ElementoBoleta>
            {
                new TextoBoleta(10f, 10f, 50f, 20f, "HOLA MUNDO", 14f, Negrita: true)
            };
            var pagina = new PaginaBoleta(100f, 50f, elementos);

            using (var g = Graphics.FromImage(bmp))
            {
                PintorBoleta.Pintar(g, pagina, null, 0f, 0f);
            }

            // Comprobar que en el área del texto hay al menos un píxel que no es blanco puro
            bool hayPixelOscuro = false;
            for (int x = 0; x < bmp.Width; x++)
            {
                for (int y = 0; y < bmp.Height; y++)
                {
                    var color = bmp.GetPixel(x, y);
                    if (color.R < 200 || color.G < 200 || color.B < 200)
                    {
                        hayPixelOscuro = true;
                        break;
                    }
                }
                if (hayPixelOscuro) break;
            }

            Assert.True(hayPixelOscuro, "PintorBoleta debió dejar píxeles oscuros al dibujar el texto.");
        });
    }

    [Fact]
    public void Pintar_ArgumentosNulos_LanzaArgumentNullException()
    {
        HiloSta.Ejecutar(() =>
        {
            using var bmp = new Bitmap(100, 100);
            using var g = Graphics.FromImage(bmp);
            var pagina = new PaginaBoleta(100f, 100f, Array.Empty<ElementoBoleta>());

            Assert.Throws<ArgumentNullException>(() => PintorBoleta.Pintar(null!, pagina, null, 0f, 0f));
            Assert.Throws<ArgumentNullException>(() => PintorBoleta.Pintar(g, null!, null, 0f, 0f));
        });
    }
}
