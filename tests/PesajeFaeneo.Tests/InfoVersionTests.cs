using System.Text.RegularExpressions;
using PesajeFaeneo.Infrastructure.Common;
using Xunit;

namespace PesajeFaeneo.Tests;

public class InfoVersionTests
{
    [Fact]
    public void RecortarInformativa_CasosLimite()
    {
        Assert.Equal("1.2.3", InfoVersion.RecortarInformativa(null, "1.2.3"));
        Assert.Equal("1.2.3", InfoVersion.RecortarInformativa("", "1.2.3"));
        Assert.Equal("1.0.0", InfoVersion.RecortarInformativa("1.0.0", "x"));
        Assert.Equal("1.0.0+abc", InfoVersion.RecortarInformativa("1.0.0+abc", "x"));
        Assert.Equal("1.0.0+0123456", InfoVersion.RecortarInformativa("1.0.0+0123456789abcdef", "x"));
    }

    [Fact]
    public void Version_CumpleFormatoSemVerMayorMenorParche()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", InfoVersion.Version);
    }

    [Fact]
    public void Version_TodosLosEnsambladosCompartenLaMismaVersion()
    {
        var versionWinForms = typeof(PesajeFaeneo.WinForms.Program).Assembly.GetName().Version?.ToString(3);
        Assert.NotNull(versionWinForms);
        Assert.Equal(versionWinForms, InfoVersion.Version);
    }
}
