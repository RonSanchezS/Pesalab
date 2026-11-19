using PesajeFaeneo.Application.Reportes;
using PesajeFaeneo.Application.Reportes.Dtos;
using PesajeFaeneo.Domain.Enums;
using Xunit;

namespace PesajeFaeneo.Tests;

public class CalculadoraTotalesReporteTests
{
    private static FilaReporte CrearFila(
        int item = 1,
        TipoDesposte tipo = TipoDesposte.ResCompleta,
        decimal izq = 100m,
        decimal der = 100m,
        decimal total = 200m,
        OrigenPeso origen = OrigenPeso.Balanza)
    {
        return new FilaReporte(
            Item: item,
            PesajeId: item,
            NumeroBoleta: 1000 + item,
            FechaHoraLocal: new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Unspecified),
            Secuencia: item,
            CodigoGanadero: "CAB",
            NombreGanadero: "GANADERO DE PRUEBA",
            CodigoCliente: 10,
            NombreCliente: "CLIENTE DE PRUEBA",
            Direccion: "DIRECCIÓN DE PRUEBA",
            TipoDesposte: tipo,
            PesoIzquierdo: izq,
            PesoDerecho: der,
            PesoTotal: total,
            Origen: origen,
            AnuladoEnLocal: null,
            AnuladoPor: null,
            MotivoAnulacion: null);
    }

    [Fact]
    public void Calcular_ListaVacia_RetornaTotalesEnCero()
    {
        var totales = CalculadoraTotalesReporte.Calcular(Array.Empty<FilaReporte>());

        Assert.Equal(0, totales.Boletas);
        Assert.Equal(0, totales.ResesCompletas);
        Assert.Equal(0, totales.MediasReses);
        Assert.Equal(0m, totales.Reses);
        Assert.Equal(0m, totales.TotalIzquierdoKg);
        Assert.Equal(0m, totales.TotalDerechoKg);
        Assert.Equal(0m, totales.TotalNetoKg);
        Assert.Equal(0, totales.Simuladas);
    }

    [Fact]
    public void Calcular_ListaNula_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CalculadoraTotalesReporte.Calcular(null!));
    }

    [Fact]
    public void Calcular_CasoCapturaLegada_CalculaTotalesExactos()
    {
        // 8 res completa de 215,0 (109 + 106) + 3 media res de 106,0 -> Boletas 11, ResesCompletas 8, MediasReses 3, Reses 9.5m, TotalNetoKg 2038.0m (R2)
        var filas = new List<FilaReporte>();
        for (int i = 1; i <= 8; i++)
        {
            filas.Add(CrearFila(item: i, tipo: TipoDesposte.ResCompleta, izq: 109m, der: 106m, total: 215m));
        }
        for (int i = 9; i <= 11; i++)
        {
            filas.Add(CrearFila(item: i, tipo: TipoDesposte.MediaRes, izq: 106m, der: 0m, total: 106m));
        }

        var totales = CalculadoraTotalesReporte.Calcular(filas);

        Assert.Equal(11, totales.Boletas);
        Assert.Equal(8, totales.ResesCompletas);
        Assert.Equal(3, totales.MediasReses);
        Assert.Equal(9.5m, totales.Reses);
        Assert.Equal(8 * 109m + 3 * 106m, totales.TotalIzquierdoKg); // 872 + 318 = 1190m
        Assert.Equal(8 * 106m, totales.TotalDerechoKg);              // 848m
        Assert.Equal(2038.0m, totales.TotalNetoKg);                  // 8*215 + 3*106 = 1720 + 318 = 2038m
        Assert.Equal(0, totales.Simuladas);
    }

    [Fact]
    public void Calcular_SoloMediasResesImpares_CalculaResesCorrectamente()
    {
        var filas = new List<FilaReporte>
        {
            CrearFila(item: 1, tipo: TipoDesposte.MediaRes, izq: 100m, der: 0m, total: 100m),
            CrearFila(item: 2, tipo: TipoDesposte.MediaRes, izq: 0m, der: 95m, total: 95m),
            CrearFila(item: 3, tipo: TipoDesposte.MediaRes, izq: 105m, der: 0m, total: 105m)
        };

        var totales = CalculadoraTotalesReporte.Calcular(filas);

        Assert.Equal(3, totales.Boletas);
        Assert.Equal(0, totales.ResesCompletas);
        Assert.Equal(3, totales.MediasReses);
        Assert.Equal(1.5m, totales.Reses);
        Assert.Equal(300m, totales.TotalNetoKg);
    }

    [Fact]
    public void Calcular_PesosConDecimales_SumaExactaSinErrorComaFlotante()
    {
        var filas = new List<FilaReporte>
        {
            CrearFila(item: 1, tipo: TipoDesposte.ResCompleta, izq: 109.3m, der: 106.7m, total: 216.0m),
            CrearFila(item: 2, tipo: TipoDesposte.ResCompleta, izq: 110.1m, der: 105.9m, total: 216.0m)
        };

        var totales = CalculadoraTotalesReporte.Calcular(filas);

        Assert.Equal(219.4m, totales.TotalIzquierdoKg);
        Assert.Equal(212.6m, totales.TotalDerechoKg);
        Assert.Equal(432.0m, totales.TotalNetoKg);
        Assert.Equal(totales.TotalNetoKg, totales.TotalIzquierdoKg + totales.TotalDerechoKg);
    }

    [Fact]
    public void Calcular_MuchasSumasDeUnDecimo_NoAcumulanErrorDeComaFlotante()
    {
        // 0,1 sumado 1 000 veces en double da 99,9999999999986: con decimal debe dar exactamente 100,0.
        var filas = Enumerable.Range(1, 1000)
            .Select(i => CrearFila(item: i, tipo: TipoDesposte.MediaRes, izq: 0.1m, der: 0m, total: 0.1m))
            .ToList();

        var totales = CalculadoraTotalesReporte.Calcular(filas);

        Assert.Equal(100.0m, totales.TotalNetoKg);
        Assert.Equal(100.0m, totales.TotalIzquierdoKg);
        Assert.Equal(500.0m, totales.Reses);
    }

    [Fact]
    public void Calcular_CuentaBoletasSimuladas()
    {
        var filas = new List<FilaReporte>
        {
            CrearFila(item: 1, origen: OrigenPeso.Balanza),
            CrearFila(item: 2, origen: OrigenPeso.Simulador),
            CrearFila(item: 3, origen: OrigenPeso.Simulador),
            CrearFila(item: 4, origen: OrigenPeso.Importado)
        };

        var totales = CalculadoraTotalesReporte.Calcular(filas);

        Assert.Equal(4, totales.Boletas);
        Assert.Equal(2, totales.Simuladas);
    }

    [Fact]
    public void Calcular_IzquierdoYDerecho_SeSumanPorSeparado()
    {
        var filas = new List<FilaReporte>
        {
            CrearFila(item: 1, izq: 120.5m, der: 115.0m, total: 235.5m),
            CrearFila(item: 2, izq: 90.0m, der: 85.5m, total: 175.5m)
        };

        var totales = CalculadoraTotalesReporte.Calcular(filas);

        Assert.Equal(210.5m, totales.TotalIzquierdoKg);
        Assert.Equal(200.5m, totales.TotalDerechoKg);
        Assert.Equal(411.0m, totales.TotalNetoKg);
    }

    [Fact]
    public void Calcular_TipoDesposteInvalido_LanzaArgumentOutOfRangeException()
    {
        var filas = new List<FilaReporte>
        {
            CrearFila(item: 1, tipo: (TipoDesposte)99)
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => CalculadoraTotalesReporte.Calcular(filas));
    }
}
