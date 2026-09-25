using PesajeFaeneo.Application.Balanza;
using Xunit;

namespace PesajeFaeneo.Tests;

public class DetectorEstabilidadTests
{
    [Fact]
    public void Agregar_N5_CuatroLecturasIgualesEsFalso_QuintaEsVerdadero()
    {
        var detector = new DetectorEstabilidad(5, 0.5m);

        Assert.False(detector.Agregar(100.0m));
        Assert.False(detector.Agregar(100.0m));
        Assert.False(detector.Agregar(100.0m));
        Assert.False(detector.Agregar(100.0m));
        Assert.True(detector.Agregar(100.0m));
        Assert.True(detector.Estable);
    }

    [Fact]
    public void Agregar_N5_RangoEnLimiteExactoEsVerdadero_MayorAlLimiteEsFalso()
    {
        var detector1 = new DetectorEstabilidad(5, 0.5m);
        // 10.0, 10.2, 10.5, 10.3, 10.1 -> min=10.0, max=10.5, dif=0.5 -> true
        Assert.False(detector1.Agregar(10.0m));
        Assert.False(detector1.Agregar(10.2m));
        Assert.False(detector1.Agregar(10.5m));
        Assert.False(detector1.Agregar(10.3m));
        Assert.True(detector1.Agregar(10.1m));

        var detector2 = new DetectorEstabilidad(5, 0.5m);
        // 10.0, 10.2, 10.6, 10.3, 10.1 -> min=10.0, max=10.6, dif=0.6 -> false
        Assert.False(detector2.Agregar(10.0m));
        Assert.False(detector2.Agregar(10.2m));
        Assert.False(detector2.Agregar(10.6m));
        Assert.False(detector2.Agregar(10.3m));
        Assert.False(detector2.Agregar(10.1m));
    }

    [Fact]
    public void Agregar_VentanaDeslizante_SeRecuperaTrasLecturaFueraDeRango()
    {
        var detector = new DetectorEstabilidad(5, 0.5m);

        for (int i = 0; i < 5; i++)
        {
            detector.Agregar(50.0m);
        }
        Assert.True(detector.Estable);

        // Lectura que rompe estabilidad
        Assert.False(detector.Agregar(60.0m));
        Assert.False(detector.Estable);

        // 4 lecturas más dentro del nuevo rango (60.0) -> tras 5 lecturas alrededor de 60 vuelve a ser estable
        Assert.False(detector.Agregar(60.2m));
        Assert.False(detector.Agregar(60.1m));
        Assert.False(detector.Agregar(60.3m));
        Assert.True(detector.Agregar(60.1m));
        Assert.True(detector.Estable);
    }

    [Fact]
    public void Agregar_EstableReportadoFalso_VaciaVentanaYRequiereNLecturasNuevas()
    {
        var detector = new DetectorEstabilidad(5, 0.5m);
        for (int i = 0; i < 5; i++)
        {
            detector.Agregar(20.0m);
        }
        Assert.True(detector.Estable);

        // Reporte inestable vacía la ventana y no se agrega
        Assert.False(detector.Agregar(20.0m, estableReportado: false));
        Assert.False(detector.Estable);

        // Requiere N lecturas nuevas
        Assert.False(detector.Agregar(20.0m));
        Assert.False(detector.Agregar(20.0m));
        Assert.False(detector.Agregar(20.0m));
        Assert.False(detector.Agregar(20.0m));
        Assert.True(detector.Agregar(20.0m));
    }

    [Fact]
    public void Agregar_EstableReportadoVerdadero_NoAceleraVentanaIncompleta()
    {
        var detector = new DetectorEstabilidad(5, 0.5m);
        Assert.False(detector.Agregar(10.0m, estableReportado: true));
        Assert.False(detector.Agregar(10.0m, estableReportado: true));
        Assert.False(detector.Agregar(10.0m, estableReportado: true));
        Assert.False(detector.Estable);
    }

    [Fact]
    public void Agregar_N1_TodaLecturaEsEstable()
    {
        var detector = new DetectorEstabilidad(1, 0.5m);
        Assert.True(detector.Agregar(15.0m));
        Assert.True(detector.Agregar(20.0m));
        Assert.True(detector.Estable);
    }

    [Fact]
    public void Agregar_ToleranciaCero_SoloLecturasIdenticasSonEstables()
    {
        var detector = new DetectorEstabilidad(3, 0m);
        detector.Agregar(10.0m);
        detector.Agregar(10.0m);
        Assert.True(detector.Agregar(10.0m));

        detector.Agregar(10.1m);
        detector.Agregar(10.0m);
        Assert.False(detector.Agregar(10.0m)); // 10.1, 10.0, 10.0 -> max-min=0.1 > 0
    }

    [Fact]
    public void Reiniciar_RestableceEstadoAInestable()
    {
        var detector = new DetectorEstabilidad(3, 0.5m);
        detector.Agregar(10.0m);
        detector.Agregar(10.0m);
        detector.Agregar(10.0m);
        Assert.True(detector.Estable);

        detector.Reiniciar();
        Assert.False(detector.Estable);
    }

    [Fact]
    public void Agregar_PesosNegativos_FuncionanIgual()
    {
        var detector = new DetectorEstabilidad(3, 0.5m);
        Assert.False(detector.Agregar(-10.0m));
        Assert.False(detector.Agregar(-10.2m));
        Assert.True(detector.Agregar(-10.1m)); // min=-10.2, max=-10.0, dif=0.2 <= 0.5
        Assert.True(detector.Estable);
    }

    [Theory]
    [InlineData(0, 0.5)]
    [InlineData(51, 0.5)]
    [InlineData(5, -0.1)]
    [InlineData(5, 10.1)]
    public void Constructor_ValoresFueraDeRango_LanzaArgumentOutOfRangeException(int lecturas, double tolerancia)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DetectorEstabilidad(lecturas, (decimal)tolerancia));
    }
}
