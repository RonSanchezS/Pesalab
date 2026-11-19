using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Reglas;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ReglasDesposteTests
{
    [Fact]
    public void Efectivo_SinDefinir_EsAmbos()
    {
        Assert.Equal(ModoDesposte.Ambos, ReglasDesposte.Efectivo(null));
        Assert.Equal(ModoDesposte.SoloResCompleta, ReglasDesposte.Efectivo(ModoDesposte.SoloResCompleta));
    }

    [Theory]
    [InlineData(ModoDesposte.SoloResCompleta, TipoDesposte.ResCompleta)]
    [InlineData(ModoDesposte.SoloMediaRes, TipoDesposte.MediaRes)]
    [InlineData(ModoDesposte.Ambos, null)]
    public void TipoUnico_SegunModo(ModoDesposte modo, TipoDesposte? esperado)
    {
        Assert.Equal(esperado, ReglasDesposte.TipoUnico(modo));
    }

    [Theory]
    [InlineData(ModoDesposte.Ambos, TipoDesposte.ResCompleta, true)]
    [InlineData(ModoDesposte.Ambos, TipoDesposte.MediaRes, true)]
    [InlineData(ModoDesposte.SoloResCompleta, TipoDesposte.ResCompleta, true)]
    [InlineData(ModoDesposte.SoloResCompleta, TipoDesposte.MediaRes, false)]
    [InlineData(ModoDesposte.SoloMediaRes, TipoDesposte.MediaRes, true)]
    [InlineData(ModoDesposte.SoloMediaRes, TipoDesposte.ResCompleta, false)]
    public void Permite_TodasLasCombinaciones(ModoDesposte modo, TipoDesposte tipo, bool esperado)
    {
        Assert.Equal(esperado, ReglasDesposte.Permite(modo, tipo));
    }

    [Fact]
    public void MostrarDesposte_Ambos_SiempreVerdadero()
    {
        Assert.True(ReglasDesposte.MostrarDesposte(ModoDesposte.Ambos, Array.Empty<TipoDesposte>()));
        Assert.True(ReglasDesposte.MostrarDesposte(ModoDesposte.Ambos, new[] { TipoDesposte.ResCompleta }));
    }

    [Fact]
    public void MostrarDesposte_ModoUnico_OcultaSiTodosCoinciden()
    {
        Assert.False(ReglasDesposte.MostrarDesposte(ModoDesposte.SoloResCompleta, Array.Empty<TipoDesposte>()));
        Assert.False(ReglasDesposte.MostrarDesposte(ModoDesposte.SoloResCompleta, new[] { TipoDesposte.ResCompleta, TipoDesposte.ResCompleta }));
        Assert.False(ReglasDesposte.MostrarDesposte(ModoDesposte.SoloMediaRes, TipoDesposte.MediaRes));
    }

    [Fact]
    public void MostrarDesposte_ModoUnico_MuestraSiHayTiposMezclados()
    {
        Assert.True(ReglasDesposte.MostrarDesposte(ModoDesposte.SoloResCompleta, new[] { TipoDesposte.ResCompleta, TipoDesposte.MediaRes }));
        Assert.True(ReglasDesposte.MostrarDesposte(ModoDesposte.SoloMediaRes, TipoDesposte.ResCompleta));
    }

    [Fact]
    public void EsValido_SoloLosTresModos()
    {
        Assert.True(ReglasDesposte.EsValido(ModoDesposte.SoloMediaRes));
        Assert.True(ReglasDesposte.EsValido(ModoDesposte.SoloResCompleta));
        Assert.True(ReglasDesposte.EsValido(ModoDesposte.Ambos));
        Assert.False(ReglasDesposte.EsValido((ModoDesposte)0));
        Assert.False(ReglasDesposte.EsValido((ModoDesposte)4));
    }

    [Fact]
    public void Texto_DeCadaModo()
    {
        Assert.Equal("Sin definir", ReglasDesposte.Texto(null));
        Assert.Equal("Solo res completa", ReglasDesposte.Texto(ModoDesposte.SoloResCompleta));
        Assert.Equal("Solo media res", ReglasDesposte.Texto(ModoDesposte.SoloMediaRes));
        Assert.Equal("Res completa y media res", ReglasDesposte.Texto(ModoDesposte.Ambos));
    }
}
