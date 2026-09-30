using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.WinForms.Impresion;
using Xunit;

namespace PesajeFaeneo.Tests.Impresion;

public class SelectorPapelTests
{
    [Fact]
    public void ConfiguradoPresente_SeleccionaEsePapel_YRespetaSensibilidad()
    {
        var papeles = new List<PapelDisponible>
        {
            new("A4", 827, 1169),
            new("Letter", 850, 1100),
            new("Statement", 550, 850)
        };

        var resultado = SelectorPapel.Elegir(FormatoBoleta.MediaCarta, "statement", papeles, 140f);

        Assert.Equal("Statement", resultado.Papel.Nombre);
        Assert.True(resultado.Horizontal);
    }

    [Fact]
    public void ConfiguradoAusente_UsaReglaAutomatica()
    {
        var papeles = new List<PapelDisponible>
        {
            new("Letter", 850, 1100)
        };

        var resultado = SelectorPapel.Elegir(FormatoBoleta.MediaCarta, "PapelInexistente", papeles, 140f);

        Assert.Equal("Letter", resultado.Papel.Nombre);
        Assert.False(resultado.Horizontal);
    }

    [Fact]
    public void MediaCarta_Statement550x850_EsHorizontal()
    {
        var papeles = new List<PapelDisponible>
        {
            new("Statement", 550, 850)
        };

        var resultado = SelectorPapel.Elegir(FormatoBoleta.MediaCarta, null, papeles, 140f);

        Assert.Equal("Statement", resultado.Papel.Nombre);
        Assert.True(resultado.Horizontal);
    }

    [Fact]
    public void MediaCarta_850x550_NoEsHorizontal()
    {
        var papeles = new List<PapelDisponible>
        {
            new("MediaCartaAcostada", 850, 550)
        };

        var resultado = SelectorPapel.Elegir(FormatoBoleta.MediaCarta, null, papeles, 140f);

        Assert.Equal("MediaCartaAcostada", resultado.Papel.Nombre);
        Assert.False(resultado.Horizontal);
    }

    [Fact]
    public void MediaCarta_SoloCarta850x1100_SeleccionaCartaVertical()
    {
        var papeles = new List<PapelDisponible>
        {
            new("Letter", 850, 1100)
        };

        var resultado = SelectorPapel.Elegir(FormatoBoleta.MediaCarta, null, papeles, 140f);

        Assert.Equal("Letter", resultado.Papel.Nombre);
        Assert.False(resultado.Horizontal);
        Assert.False(resultado.Papel.EsPersonalizado);
    }

    [Fact]
    public void MediaCarta_SinPapelesApropiados_CreaPersonalizado()
    {
        var papeles = new List<PapelDisponible>
        {
            new("A4", 827, 1169)
        };

        var resultado = SelectorPapel.Elegir(FormatoBoleta.MediaCarta, null, papeles, 140f);

        Assert.Equal("Media carta (personalizado)", resultado.Papel.Nombre);
        Assert.True(resultado.Papel.EsPersonalizado);
        Assert.Equal(850, resultado.Papel.AnchoCentPulg);
        Assert.Equal(550, resultado.Papel.AltoCentPulg);
        Assert.False(resultado.Horizontal);
    }

    [Fact]
    public void Ticket80mm_PapelConAncho315_SeleccionaEsePapel()
    {
        var papeles = new List<PapelDisponible>
        {
            new("80mm x 297mm", 315, 1169),
            new("Letter", 850, 1100)
        };

        var resultado = SelectorPapel.Elegir(FormatoBoleta.Ticket80mm, null, papeles, 150f);

        Assert.Equal("80mm x 297mm", resultado.Papel.Nombre);
        Assert.False(resultado.Horizontal);
        Assert.False(resultado.Papel.EsPersonalizado);
    }

    [Fact]
    public void Ticket80mm_SinPapelConAncho315_CreaPersonalizadoConAltoCalculado()
    {
        var papeles = new List<PapelDisponible>
        {
            new("Letter", 850, 1100)
        };

        float altoContenidoMm = 120f;
        int altoEsperado = (int)Math.Ceiling(altoContenidoMm / 0.254f);

        var resultado = SelectorPapel.Elegir(FormatoBoleta.Ticket80mm, null, papeles, altoContenidoMm);

        Assert.Equal("Ticket 80 mm (personalizado)", resultado.Papel.Nombre);
        Assert.True(resultado.Papel.EsPersonalizado);
        Assert.Equal(315, resultado.Papel.AnchoCentPulg);
        Assert.Equal(altoEsperado, resultado.Papel.AltoCentPulg);
        Assert.False(resultado.Horizontal);
    }
}
