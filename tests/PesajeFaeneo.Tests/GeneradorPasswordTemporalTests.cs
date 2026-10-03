using PesajeFaeneo.Application.Seguridad;
using Xunit;

namespace PesajeFaeneo.Tests;

public class GeneradorPasswordTemporalTests
{
    [Fact]
    public void Generar_PorDefecto_TieneDiezCaracteres()
    {
        Assert.Equal(10, GeneradorPasswordTemporal.Generar().Length);
    }

    [Fact]
    public void Generar_SoloUsaCaracteresDelAlfabetoSinAmbiguos()
    {
        for (var i = 0; i < 50; i++)
        {
            var password = GeneradorPasswordTemporal.Generar(30);
            Assert.All(password, c => Assert.Contains(c, GeneradorPasswordTemporal.Alfabeto));
        }

        Assert.DoesNotContain('0', GeneradorPasswordTemporal.Alfabeto);
        Assert.DoesNotContain('O', GeneradorPasswordTemporal.Alfabeto);
        Assert.DoesNotContain('1', GeneradorPasswordTemporal.Alfabeto);
        Assert.DoesNotContain('l', GeneradorPasswordTemporal.Alfabeto);
        Assert.DoesNotContain('I', GeneradorPasswordTemporal.Alfabeto);
    }

    [Fact]
    public void Generar_DosCientasDeVeces_ProduceValoresDistintos()
    {
        var distintas = Enumerable.Range(0, 200).Select(_ => GeneradorPasswordTemporal.Generar()).Distinct().Count();

        Assert.True(distintas >= 199);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(101)]
    public void Generar_LongitudFueraDeLaPolitica_Lanza(int longitud)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GeneradorPasswordTemporal.Generar(longitud));
    }
}
