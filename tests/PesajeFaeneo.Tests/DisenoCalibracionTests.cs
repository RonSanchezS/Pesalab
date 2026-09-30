using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Application.Boletas.Dtos;
using Xunit;

namespace PesajeFaeneo.Tests;

public class DisenoCalibracionTests
{
    [Theory]
    [InlineData(FormatoBoleta.MediaCarta, 216f, 140f)]
    [InlineData(FormatoBoleta.Ticket80mm, 80f, 120f)]
    public void Crear_PrimerRectanguloEn10_10YDimensionesCorrectas(FormatoBoleta formato, float ancho, float alto)
    {
        var pagina = DisenoCalibracion.Crear(formato);

        Assert.Equal(ancho, pagina.AnchoMm);
        Assert.Equal(alto, pagina.AltoMm);

        var primerRect = Assert.IsType<RectanguloBoleta>(pagina.Elementos[0]);
        Assert.Equal(10f, primerRect.X);
        Assert.Equal(10f, primerRect.Y);
        Assert.Equal(ancho - 20f, primerRect.Ancho);
        Assert.Equal(alto - 20f, primerRect.Alto);

        // Verificar que todos los elementos están dentro de la página
        foreach (var el in pagina.Elementos)
        {
            switch (el)
            {
                case TextoBoleta t:
                    Assert.True(t.X >= 0);
                    Assert.True(t.Y >= 0);
                    Assert.True(t.X + t.Ancho <= pagina.AnchoMm + 0.01f);
                    Assert.True(t.Y + t.Alto <= pagina.AltoMm + 0.01f);
                    break;
                case RectanguloBoleta r:
                    Assert.True(r.X >= 0);
                    Assert.True(r.Y >= 0);
                    Assert.True(r.X + r.Ancho <= pagina.AnchoMm + 0.01f);
                    Assert.True(r.Y + r.Alto <= pagina.AltoMm + 0.01f);
                    break;
                case LineaBoleta l:
                    Assert.True(l.X1 >= 0 && l.X1 <= pagina.AnchoMm + 0.01f);
                    Assert.True(l.X2 >= 0 && l.X2 <= pagina.AnchoMm + 0.01f);
                    Assert.True(l.Y1 >= 0 && l.Y1 <= pagina.AltoMm + 0.01f);
                    Assert.True(l.Y2 >= 0 && l.Y2 <= pagina.AltoMm + 0.01f);
                    break;
            }
        }
    }

    [Theory]
    [InlineData(FormatoBoleta.MediaCarta)]
    [InlineData(FormatoBoleta.Ticket80mm)]
    public void Crear_InstruccionesNoPisanLasReglasYLasLargasVanEnVariasLineas(FormatoBoleta formato)
    {
        var textos = DisenoCalibracion.Crear(formato).Elementos.OfType<TextoBoleta>().ToList();
        var numerosRegla = textos.Where(t => t.Texto.All(char.IsDigit)).ToList();
        var instrucciones = textos.Except(numerosRegla).ToList();

        Assert.NotEmpty(numerosRegla);
        Assert.Equal(4, instrucciones.Count);

        // Regla superior: números centrados sobre cada marca; regla izquierda: alineados a la izquierda.
        var bordeInferiorReglaSuperior = numerosRegla.Where(t => t.Alineacion == AlineacionTexto.Centro).Max(t => t.Y + t.Alto);
        var bordeDerechoReglaIzquierda = numerosRegla.Where(t => t.Alineacion == AlineacionTexto.Izquierda).Max(t => t.X + t.Ancho);
        foreach (var t in instrucciones)
        {
            Assert.True(t.X >= bordeDerechoReglaIzquierda, $"'{t.Texto}' pisa la regla izquierda (X = {t.X}).");
            Assert.True(t.Y >= bordeInferiorReglaSuperior, $"'{t.Texto}' pisa la regla superior (Y = {t.Y}).");
        }

        var larga = Assert.Single(instrucciones, t => t.Texto.StartsWith("Si queda", StringComparison.Ordinal));
        Assert.True(larga.VariasLineas);
    }
}
