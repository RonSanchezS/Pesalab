using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Reglas;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ReglasReportesTests
{
    [Fact]
    public void Reses_ResCompleta_EquivaleAUno()
    {
        var reses = ReglasReportes.Reses(TipoDesposte.ResCompleta);
        Assert.Equal(1.0m, reses);
    }

    [Fact]
    public void Reses_MediaRes_EquivaleAMedio()
    {
        var reses = ReglasReportes.Reses(TipoDesposte.MediaRes);
        Assert.Equal(0.5m, reses);
    }

    [Fact]
    public void Reses_TipoDesposteInvalido_LanzaArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ReglasReportes.Reses((TipoDesposte)99));
    }

    [Fact]
    public void LimitesFilas_MantienenJerarquiaEstricta()
    {
        Assert.True(ReglasReportes.MaximoFilasVista < ReglasReportes.MaximoFilasImpresion);
        Assert.True(ReglasReportes.MaximoFilasImpresion < ReglasReportes.MaximoFilas);
        Assert.Equal(1000, ReglasReportes.MaximoFilasVista);
        Assert.Equal(2000, ReglasReportes.MaximoFilasImpresion);
        Assert.Equal(20000, ReglasReportes.MaximoFilas);
        Assert.Equal(200, ReglasReportes.ResolucionImpresionDpi);
    }
}
