using System.Diagnostics;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Domain.Enums;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ParserTramasTests
{
    private const string P0 = @"(?<peso>[-+]?\s*\d+(?:[.,]\d+)?)";
    private const string P1 = @"^(?<estable>ST)?[^-+\d]*(?<peso>[-+]?\s*\d+(?:[.,]\d+)?)";

    [Theory]
    [InlineData(P0, "  123.4", true, 123.4, null)]
    [InlineData(P0, "+0123.45 kg", true, 123.5, null)]
    [InlineData(P0, "-5,2", true, -5.2, null)]
    [InlineData(P0, "-   12.5", true, -12.5, null)]
    [InlineData(P0, "0", true, 0.0, null)]
    [InlineData(P0, "ABC", false, null, null)]
    [InlineData(P0, "", false, null, null)]
    [InlineData(P0, null, false, null, null)]
    [InlineData(P0, "123456", false, null, null)]
    [InlineData(P0, "1,234.5", false, null, null)]
    [InlineData(P1, "ST,GS,+  98.6kg", true, 98.6, true)]
    [InlineData(P1, "US,GS,+  98.6kg", true, 98.6, false)]
    [InlineData(P1, "ST,GS,-   0.4kg", true, -0.4, true)]
    public void Parsear_CasosEspecificados_RetornaResultadoEsperado(
        string patron,
        string? trama,
        bool validaEsperada,
        object? pesoEsperado,
        bool? estableEsperado)
    {
        var parser = new ParserTramas(patron);
        var resultado = parser.Parsear(trama);

        Assert.Equal(validaEsperada, resultado.Valida);
        if (pesoEsperado is not null)
        {
            Assert.Equal(Convert.ToDecimal(pesoEsperado), resultado.PesoKg);
        }
        else
        {
            Assert.Null(resultado.PesoKg);
        }
        Assert.Equal(estableEsperado, resultado.EstableReportado);
    }

    [Theory]
    [InlineData(null, "Ingrese el patrón de lectura.")]
    [InlineData("", "Ingrese el patrón de lectura.")]
    [InlineData("   ", "Ingrese el patrón de lectura.")]
    public void ValidarPatron_Vacio_RetornaMensajeEsperado(string? patron, string mensajeEsperado)
    {
        var error = ParserTramas.ValidarPatron(patron);
        Assert.Equal(mensajeEsperado, error);
    }

    [Fact]
    public void ValidarPatron_LongitudMayorA250_RetornaMensajeEsperado()
    {
        var patronLargo = "(?<peso>\\d+)" + new string('a', 251);
        var error = ParserTramas.ValidarPatron(patronLargo);
        Assert.Equal("El patrón no puede superar 250 caracteres.", error);
    }

    [Fact]
    public void ValidarPatron_RegexInvalido_RetornaMensajeEsperado()
    {
        var error = ParserTramas.ValidarPatron("(?<peso>[a-z");
        Assert.Equal("El patrón no es una expresión regular válida.", error);
    }

    [Fact]
    public void ValidarPatron_SinGrupoPeso_RetornaMensajeEsperado()
    {
        var error = ParserTramas.ValidarPatron(@"\d+(\.\d+)?");
        Assert.Equal("El patrón debe tener un grupo con nombre (?<peso>...).", error);
    }

    [Theory]
    [InlineData(P0)]
    [InlineData(P1)]
    public void ValidarPatron_PatronesValidos_RetornaNull(string patron)
    {
        var error = ParserTramas.ValidarPatron(patron);
        Assert.Null(error);
    }

    [Fact]
    public void Constructor_PatronInvalido_LanzaArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new ParserTramas(@"\d+"));
        Assert.Equal("El patrón debe tener un grupo con nombre (?<peso>...). (Parameter 'patron')", ex.Message);
    }

    [Fact]
    public void Parsear_RegexCatastrofico_RetornaInvalidoSinLanzarYEnMenosDe2Segundos()
    {
        var parser = new ParserTramas(@"(?<peso>(a+)+)b");
        var entrada = new string('a', 5000);

        var sw = Stopwatch.StartNew();
        var resultado = parser.Parsear(entrada);
        sw.Stop();

        Assert.False(resultado.Valida);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"La ejecución tardó {sw.ElapsedMilliseconds} ms, superando los 2 s.");
    }

    private const string PatronSembrado = @"^[^-+\d]*(?<peso>[-+]?\s*\d+(?:[.,]\d+)?)";

    [Theory]
    [InlineData("=000000", 0.0)]
    [InlineData("=012345", 12.3)]   // 12345 g -> 12,345 kg -> redondeo a 0,1 kg
    [InlineData("=012350", 12.4)]   // punto medio: AwayFromZero
    [InlineData("=450000", 450.0)]
    [InlineData("=-00500", -0.5)]
    public void Parsear_TramaRx8EnGramos_ConPatronSembrado_ConvierteAKg(string trama, double kgEsperado)
    {
        var parser = new ParserTramas(PatronSembrado, UnidadPesoTrama.Gramos);

        var r = parser.Parsear(trama);

        Assert.True(r.Valida, r.Error);
        Assert.Equal((decimal)kgEsperado, r.PesoKg);
        Assert.Null(r.EstableReportado);
    }

    [Fact]
    public void Parsear_Gramos_ElRangoSeValidaDespuesDeConvertir()
    {
        // 450000 g son 450 kg (válido); en kg el mismo número superaría el máximo admitido.
        Assert.True(new ParserTramas(PatronSembrado, UnidadPesoTrama.Gramos).Parsear("=450000").Valida);
        Assert.False(new ParserTramas(PatronSembrado, UnidadPesoTrama.Kilogramos).Parsear("=450000").Valida);
    }

    [Fact]
    public void Constructor_UnidadInvalida_LanzaArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParserTramas(PatronSembrado, (UnidadPesoTrama)99));
    }
}
