using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Reglas;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ReglasPesajeTests
{
    [Fact]
    public void Redondear_UsaAwayFromZero()
    {
        Assert.Equal(100.1m, ReglasPesaje.Redondear(100.05m));
        Assert.Equal(100.0m, ReglasPesaje.Redondear(100.04m));
        Assert.Equal(100.2m, ReglasPesaje.Redondear(100.15m));
    }

    [Fact]
    public void CalcularTotal_RedondeaPorLadoAntesDeSumar()
    {
        // 100.05 -> 100.1, 100.04 -> 100.0, total -> 200.1
        var total = ReglasPesaje.CalcularTotal(100.05m, 100.04m);
        Assert.Equal(200.1m, total);
    }

    [Theory]
    [InlineData('M', true)]
    [InlineData('m', true)]
    [InlineData('H', true)]
    [InlineData('h', true)]
    [InlineData('X', false)]
    [InlineData(' ', false)]
    [InlineData('\0', false)]
    public void EsSexoValido_ValidaCorrectamente(char sexo, bool esperado)
    {
        Assert.Equal(esperado, ReglasPesaje.EsSexoValido(sexo));
    }

    [Theory]
    [InlineData(10.0, true)]
    [InlineData(600.0, true)]
    [InlineData(9.9, false)]
    [InlineData(600.1, false)]
    [InlineData(0.0, false)]
    [InlineData(-5.0, false)]
    public void EsPesoLadoValido_LimitesExactos(decimal peso, bool esperado)
    {
        Assert.Equal(esperado, ReglasPesaje.EsPesoLadoValido(peso));
    }

    [Fact]
    public void SugerirSecuencia_CasosLimite()
    {
        Assert.Equal(1, ReglasPesaje.SugerirSecuencia(null));
        Assert.Equal(5, ReglasPesaje.SugerirSecuencia(4));
        Assert.Equal(9999, ReglasPesaje.SugerirSecuencia(9999));
    }

    [Fact]
    public void ValidarPesos_TipoDesposteInvalido_DevuelveErrorDesposte()
    {
        var errores = ReglasPesaje.ValidarPesos((TipoDesposte)3, 100m, 100m);
        var err = Assert.Single(errores);
        Assert.Equal(CamposPesaje.TipoDesposte, err.Campo);
        Assert.Equal("Seleccione el tipo de desposte.", err.Mensaje);
    }

    [Fact]
    public void ValidarPesos_PesosNegativos_DevuelveErrorNegativo()
    {
        var errores = ReglasPesaje.ValidarPesos(TipoDesposte.ResCompleta, -1m, 100m);
        var err = Assert.Single(errores);
        Assert.Equal(CamposPesaje.Pesos, err.Campo);
        Assert.Equal("Los pesos no pueden ser negativos.", err.Mensaje);
    }

    [Fact]
    public void ValidarPesos_AmbosEnCero_DevuelveErrorAlMenosUno()
    {
        var errores = ReglasPesaje.ValidarPesos(TipoDesposte.ResCompleta, 0m, 0m);
        var err = Assert.Single(errores);
        Assert.Equal(CamposPesaje.Pesos, err.Campo);
        Assert.Equal("Capture al menos un peso.", err.Mensaje);
    }

    [Fact]
    public void ValidarPesos_ResCompleta_FaltaIzquierdo_DevuelveError()
    {
        var errores = ReglasPesaje.ValidarPesos(TipoDesposte.ResCompleta, 0m, 120m);
        var err = Assert.Single(errores);
        Assert.Equal(CamposPesaje.PesoIzquierdo, err.Campo);
        Assert.Equal("Res completa: capture el peso izquierdo.", err.Mensaje);
    }

    [Fact]
    public void ValidarPesos_ResCompleta_FaltaDerecho_DevuelveError()
    {
        var errores = ReglasPesaje.ValidarPesos(TipoDesposte.ResCompleta, 120m, 0m);
        var err = Assert.Single(errores);
        Assert.Equal(CamposPesaje.PesoDerecho, err.Campo);
        Assert.Equal("Res completa: capture el peso derecho.", err.Mensaje);
    }

    [Fact]
    public void ValidarPesos_ResCompleta_LadoFueraDeRango_DevuelveErrorConRango()
    {
        var errores = ReglasPesaje.ValidarPesos(TipoDesposte.ResCompleta, 9.9m, 600.1m);
        Assert.Equal(2, errores.Count);
        Assert.Equal(CamposPesaje.PesoIzquierdo, errores[0].Campo);
        Assert.Equal("El peso de cada lado debe estar entre 10,0 y 600,0 kg.", errores[0].Mensaje);
        Assert.Equal(CamposPesaje.PesoDerecho, errores[1].Campo);
        Assert.Equal("El peso de cada lado debe estar entre 10,0 y 600,0 kg.", errores[1].Mensaje);
    }

    [Fact]
    public void ValidarPesos_MediaRes_AmbosLadosCapturados_DevuelveError()
    {
        var errores = ReglasPesaje.ValidarPesos(TipoDesposte.MediaRes, 120m, 130m);
        var err = Assert.Single(errores);
        Assert.Equal(CamposPesaje.Pesos, err.Campo);
        Assert.Equal("Media res: capture solo un lado.", err.Mensaje);
    }

    [Fact]
    public void ValidarPesos_MediaRes_IzquierdoFueraDeRango_DevuelveError()
    {
        var errores = ReglasPesaje.ValidarPesos(TipoDesposte.MediaRes, 9.9m, 0m);
        var err = Assert.Single(errores);
        Assert.Equal(CamposPesaje.PesoIzquierdo, err.Campo);
        Assert.Equal("El peso de cada lado debe estar entre 10,0 y 600,0 kg.", err.Mensaje);
    }

    [Fact]
    public void ValidarPesos_MediaRes_DerechoFueraDeRango_DevuelveError()
    {
        var errores = ReglasPesaje.ValidarPesos(TipoDesposte.MediaRes, 0m, 600.1m);
        var err = Assert.Single(errores);
        Assert.Equal(CamposPesaje.PesoDerecho, err.Campo);
        Assert.Equal("El peso de cada lado debe estar entre 10,0 y 600,0 kg.", err.Mensaje);
    }

    [Fact]
    public void ValidarPesos_CasosValidos_DevuelvenVacio()
    {
        Assert.Empty(ReglasPesaje.ValidarPesos(TipoDesposte.ResCompleta, 120m, 130m));
        Assert.Empty(ReglasPesaje.ValidarPesos(TipoDesposte.MediaRes, 120m, 0m));
        Assert.Empty(ReglasPesaje.ValidarPesos(TipoDesposte.MediaRes, 0m, 120m));
    }
}
