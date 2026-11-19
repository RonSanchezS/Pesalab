using System.Text;
using PesajeFaeneo.WinForms;
using Xunit;

namespace PesajeFaeneo.Tests;

/// <summary>
/// El build de capturas para los manuales (constante CAPTURAS_MANUAL) nunca debe llegar al producto: estos tests corren en el
/// build normal y fallan si la constante se cuela o si el código de capturas queda compilado en los ensamblados.
/// </summary>
public class CapturasManualTests
{
    [Fact]
    public void BuildNormal_NoDefineLaConstanteCapturasManual()
    {
#if CAPTURAS_MANUAL
        Assert.Fail("CAPTURAS_MANUAL solo se define con -p:CapturasManual=true (scripts/capturas-manual.ps1), nunca en el build normal ni en la publicación.");
#endif
        Assert.True(true);
    }

    [Fact]
    public void BuildNormal_NoCompilaLasClasesDeCapturas()
    {
        var winForms = typeof(Program).Assembly;
        var infraestructura = typeof(PesajeFaeneo.Infrastructure.DependencyInjection).Assembly;

        Assert.DoesNotContain(winForms.GetTypes(), t => t.Namespace is not null && t.Namespace.EndsWith(".Capturas", StringComparison.Ordinal));
        Assert.DoesNotContain(infraestructura.GetTypes(), t => t.FullName is not null && t.FullName.Contains(".Balanza.Demo.", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("--datos")]
    [InlineData("--capturas")]
    [InlineData("PesajeFaeneo-Capturas")]
    public void BuildNormal_ElEjecutableNoReconoceLosArgumentosDeCapturas(string literal)
    {
        // Los literales de C# van al montón de cadenas del ensamblado en UTF-16: si Program los reconociera, estarían ahí.
        var bytes = File.ReadAllBytes(typeof(Program).Assembly.Location);
        var buscado = Encoding.Unicode.GetBytes(literal);
        Assert.Equal(-1, bytes.AsSpan().IndexOf(buscado));
    }
}
