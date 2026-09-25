using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Balanza;
using PesajeFaeneo.Infrastructure.Balanza.Simulador;
using PesajeFaeneo.Tests.Fixtures;
using PesajeFaeneo.Tests.Utilidades.Balanza;
using Xunit;

namespace PesajeFaeneo.Tests.Balanza;

public class SimuladorBalanzaTests
{

    [Fact]
    public void GeneradorPesoSimulado_Desde0Hacia120_ConvergeEnMenosDe40Pasos()
    {
        // Nota: no se usa "actual != objetivo" como condición de corte del bucle de convergencia, porque
        // durante la fase oscilante (previa al asentamiento determinístico por envolvente) el ruido
        // aleatorio podría, por coincidencia, cruzar exactamente el objetivo antes de asentarse de verdad
        // y luego alejarse de nuevo, lo que rompería falsamente la aserción de "se queda en el objetivo".
        var random = new Random(42);
        var generador = new GeneradorPesoSimulado();
        decimal actual = 0m;
        decimal objetivo = 120m;

        for (int i = 0; i < 40; i++)
        {
            actual = generador.Siguiente(actual, objetivo, random);
        }

        Assert.Equal(objetivo, actual);

        // Se queda en 120 en los pasos siguientes
        for (int i = 0; i < 10; i++)
        {
            actual = generador.Siguiente(actual, objetivo, random);
            Assert.Equal(objetivo, actual);
        }
    }

    [Fact]
    public void GeneradorPesoSimulado_Desde120Hacia0_ConvergeEnMenosDe40Pasos()
    {
        var random = new Random(42);
        var generador = new GeneradorPesoSimulado();
        decimal actual = 120m;
        decimal objetivo = 0m;

        for (int i = 0; i < 40; i++)
        {
            actual = generador.Siguiente(actual, objetivo, random);
        }

        Assert.Equal(objetivo, actual);

        for (int i = 0; i < 10; i++)
        {
            actual = generador.Siguiente(actual, objetivo, random);
            Assert.Equal(objetivo, actual);
        }
    }

    [Fact]
    public void GeneradorPesoSimulado_ConObjetivoIgualActual_DevuelveSiempreObjetivo()
    {
        var random = new Random(123);
        var generador = new GeneradorPesoSimulado();
        decimal objetivo = 75.5m;
        for (int i = 0; i < 50; i++)
        {
            decimal res = generador.Siguiente(objetivo, objetivo, random);
            Assert.Equal(objetivo, res);
        }
    }

    [Fact]
    public void GeneradorPesoSimulado_NuncaSuperaLimites()
    {
        var random = new Random(999);
        var generador = new GeneradorPesoSimulado();
        decimal actual = -5m;
        decimal objetivo = -100m; // fuera del rango

        for (int i = 0; i < 50; i++)
        {
            actual = generador.Siguiente(actual, objetivo, random);
            Assert.True(actual >= -10m);
        }

        var generador2 = new GeneradorPesoSimulado();
        actual = 1990m;
        objetivo = 3000m; // mayor a PesoMaximoSimuladorKg
        for (int i = 0; i < 50; i++)
        {
            actual = generador2.Siguiente(actual, objetivo, random);
            Assert.True(actual <= ReglasBalanza.PesoMaximoSimuladorKg);
        }
    }

    [Fact]
    public void GeneradorPesoSimulado_D10_OscilaAntesDeAsentarse()
    {
        // D10: debe comportarse como una senoidal amortiguada (con overshoot), no una aproximación
        // monótona directa. Se verifica que la desviación respecto al objetivo cambia de signo
        // (cruza el objetivo) al menos una vez antes de asentarse, para un salto grande.
        var random = new Random(7);
        var generador = new GeneradorPesoSimulado();
        decimal actual = 0m;
        decimal objetivo = 100m;

        bool huboSignoPositivo = false;
        bool huboSignoNegativo = false;

        for (int i = 0; i < 40 && actual != objetivo; i++)
        {
            actual = generador.Siguiente(actual, objetivo, random);
            decimal desviacion = actual - objetivo;
            if (desviacion > 0m) huboSignoPositivo = true;
            if (desviacion < 0m) huboSignoNegativo = true;
        }

        Assert.True(huboSignoPositivo && huboSignoNegativo,
            "Se esperaba que la trayectoria oscilara (overshoot) alrededor del objetivo antes de asentarse.");
    }

    [Fact]
    public void GeneradorPesoSimulado_CambioDeObjetivoAMitadDeCamino_ReiniciaLaOscilacionDesdeLaPosicionActual()
    {
        var random = new Random(11);
        var generador = new GeneradorPesoSimulado();
        decimal actual = 0m;

        for (int i = 0; i < 5; i++)
        {
            actual = generador.Siguiente(actual, 100m, random);
        }

        // Cambia el objetivo antes de asentarse: no debe saltar instantáneamente, sigue acotado.
        decimal previo = actual;
        actual = generador.Siguiente(actual, 50m, random);
        Assert.True(Math.Abs(actual - previo) < 200m);

        for (int i = 0; i < 40 && actual != 50m; i++)
        {
            actual = generador.Siguiente(actual, 50m, random);
        }

        Assert.Equal(50m, actual);
    }

    [Fact]
    public void SimuladorBalanza_EstablecerPesoObjetivo_RecortaYRedondea()
    {
        var simulador = new SimuladorBalanza(NullLogger<SimuladorBalanza>.Instance);

        simulador.EstablecerPesoObjetivo(-5m);
        Assert.Equal(0m, simulador.PesoObjetivoKg);

        simulador.EstablecerPesoObjetivo(3000m);
        Assert.Equal(ReglasBalanza.PesoMaximoSimuladorKg, simulador.PesoObjetivoKg);

        simulador.EstablecerPesoObjetivo(123.456m);
        Assert.Equal(123.5m, simulador.PesoObjetivoKg);
    }

    [Fact]
    public async Task LectorSimulador_AvanzandoTiempo_ProduceTramasParseables()
    {
        var simulador = new SimuladorBalanza(NullLogger<SimuladorBalanza>.Instance);
        simulador.EstablecerPesoObjetivo(100m);

        var timeProvider = new FakeTimeProvider();
        var lector = new LectorSimulador(simulador, timeProvider, NullLogger.Instance, new Random(42));
        await lector.ConectarAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var lecturaTask = lector.LeerTramaAsync(cts.Token).AsTask();
        await Esperar.AvanzandoHastaAsync(timeProvider, TimeSpan.FromMilliseconds(50), () => lecturaTask.IsCompleted, "esperando trama del simulador");
        string trama = await lecturaTask;

        Assert.NotEmpty(trama);
        var parser = new ParserTramas(@"(?<peso>[-+]?\s*\d+(?:[.,]\d+)?)");
        var resultado = parser.Parsear(trama);
        Assert.True(resultado.Valida);
        Assert.NotNull(resultado.PesoKg);

        await lector.DisposeAsync();
    }

    [Fact]
    public async Task LectorSimulador_DesconexionSimulada_LanzaConexionBalanzaException()
    {
        var simulador = new SimuladorBalanza(NullLogger<SimuladorBalanza>.Instance)
        {
            DesconexionSimulada = true
        };

        var timeProvider = new FakeTimeProvider();
        var lector = new LectorSimulador(simulador, timeProvider, NullLogger.Instance);

        // ConectarAsync falla si ya estaba en desconexión
        await Assert.ThrowsAsync<ConexionBalanzaException>(() => lector.ConectarAsync(CancellationToken.None));

        // Desconexión a mitad de lectura
        simulador.DesconexionSimulada = false;
        var lector2 = new LectorSimulador(simulador, timeProvider, NullLogger.Instance);
        await lector2.ConectarAsync(CancellationToken.None);

        simulador.DesconexionSimulada = true;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var lecturaTask = lector2.LeerTramaAsync(cts.Token).AsTask();
        await Esperar.AvanzandoHastaAsync(timeProvider, TimeSpan.FromMilliseconds(50), () => lecturaTask.IsCompleted, "esperando desconexión simulada");

        var ex = await Assert.ThrowsAsync<ConexionBalanzaException>(() => lecturaTask);
        Assert.Contains("Desconexión simulada.", ex.Message);

        await lector2.DisposeAsync();
    }

    [Fact]
    public async Task IntegracionExtremoAExtremo_ServicioBalanzaConFabricaYSimulador_LlegaAEstable()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var config = await ctx.ConfiguracionesBalanza.SingleAsync(c => c.Id == 1);
            config.TipoConexion = TipoConexion.Simulador;
            config.LecturasParaEstable = 3;
            config.ToleranciaKg = 0.5m;
            config.PatronRegex = @"(?<peso>[-+]?\s*\d+(?:[.,]\d+)?)";
            await ctx.SaveChangesAsync();
        }

        var simulador = new SimuladorBalanza(NullLogger<SimuladorBalanza>.Instance);
        simulador.EstablecerPesoObjetivo(150m);

        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero));
        var fabrica = new FabricaLectorBalanza(simulador, timeProvider, NullLoggerFactory.Instance);

        var sesion = new SesionActual();

        using var servicio = new ServicioBalanza(db.UnidadDeTrabajoFactory, fabrica, sesion, timeProvider, NullLogger<ServicioBalanza>.Instance);
        await servicio.IniciarAsync();

        // Avanzar el tiempo para que el simulador envíe tramas
        await Esperar.AvanzandoHastaAsync(timeProvider, TimeSpan.FromMilliseconds(200), () =>
            servicio.Instantanea.Estado == EstadoBalanza.Conectada &&
            servicio.Instantanea.Estable &&
            servicio.Instantanea.PesoKg == 150.0m,
            "esperando que el servicio alcance estabilidad en 150 kg");

        Assert.Equal(EstadoBalanza.Conectada, servicio.Instantanea.Estado);
        Assert.True(servicio.Instantanea.Estable);
        Assert.Equal(150.0m, servicio.Instantanea.PesoKg);

        await servicio.DetenerAsync();
    }
}
