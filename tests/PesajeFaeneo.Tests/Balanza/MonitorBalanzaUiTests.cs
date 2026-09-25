using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Tests.Utilidades.Balanza;
using PesajeFaeneo.WinForms.Infraestructura;
using Xunit;

namespace PesajeFaeneo.Tests.Balanza;

public class MonitorBalanzaUiTests
{
    private static InstantaneaBalanza CrearInstantanea(long secuencia, EstadoBalanza estado = EstadoBalanza.Conectada) => new(
        Secuencia: secuencia,
        Estado: estado,
        PesoKg: 100m,
        Estable: true,
        InstanteLectura: DateTimeOffset.UtcNow,
        Mensaje: null,
        Descripcion: "COM1",
        EsSimulador: false);

    [Fact]
    public void Sondear_SinCambioDeSecuencia_NoDisparaEvento_ConCambioLoDisparaUnaVez()
    {
        var servicio = new ServicioBalanzaFalso();
        servicio.Instantanea = CrearInstantanea(1);

        using var monitor = new MonitorBalanzaUi(servicio);
        int disparos = 0;
        InstantaneaBalanza? recibida = null;
        monitor.Actualizado += (_, inst) =>
        {
            disparos++;
            recibida = inst;
        };

        monitor.Sondear();
        Assert.Equal(1, disparos);
        Assert.Equal(1, recibida?.Secuencia);

        // Segundo sondeo sin cambio de secuencia
        monitor.Sondear();
        Assert.Equal(1, disparos);

        // Nuevo sondeo con secuencia distinta
        servicio.Instantanea = CrearInstantanea(2);
        monitor.Sondear();
        Assert.Equal(2, disparos);
        Assert.Equal(2, recibida?.Secuencia);
    }

    [Fact]
    public void Sondear_CuandoManejadorLanza_NoPropagaExcepcionYSiguienteSondeoFunciona()
    {
        var servicio = new ServicioBalanzaFalso();
        servicio.Instantanea = CrearInstantanea(1);

        using var monitor = new MonitorBalanzaUi(servicio);
        bool primeraVez = true;
        int recibidas = 0;

        monitor.Actualizado += (_, _) =>
        {
            recibidas++;
            if (primeraVez)
            {
                primeraVez = false;
                throw new InvalidOperationException("Fallo intencional en manejador.");
            }
        };

        // El primer sondeo lanza excepción internamente pero no debe propagarse fuera de Sondear
        monitor.Sondear();
        Assert.Equal(1, recibidas);

        // El siguiente sondeo con cambio de secuencia debe volver a invocar el evento normalmente
        servicio.Instantanea = CrearInstantanea(2);
        monitor.Sondear();
        Assert.Equal(2, recibidas);
    }

    [Fact]
    public void Dispose_DetieneYNoVuelveADisparar_YSegundoDisposeNoLanza()
    {
        var servicio = new ServicioBalanzaFalso();
        servicio.Instantanea = CrearInstantanea(1);

        var monitor = new MonitorBalanzaUi(servicio);
        int disparos = 0;
        monitor.Actualizado += (_, _) => disparos++;

        monitor.Dispose();
        monitor.Dispose(); // Idempotente

        monitor.Sondear();
        Assert.Equal(0, disparos);
    }
}
